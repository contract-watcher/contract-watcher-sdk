using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Options;
using ContractWatcher.SDK.Contracts.Policies;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Services;
using ContractWatcher.SDK.Scanning.Enums;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Contracts.Stores;

/// <summary>
/// Управляет сохранённым состоянием доставки контрактов.
/// </summary>
/// <remarks>
/// <para>
/// Использует существующий DatabaseAccess.
/// HTTP-запросы и публикацию контрактов не выполняет.
/// </para>
/// <para>
/// За пределы операций над базой возвращаются только независимые снимки.
/// </para>
/// </remarks>
internal sealed class ContractDeliveryStore(
    DatabaseAccess database, 
    ContractPublicationRetryPolicy policy, 
    IOptions<ContractPublicationOptions> options, 
    TimeProvider clock)
{
    /// <summary>
    /// Получает партию контрактов, готовых к обработке.
    /// </summary>
    /// <remarks>
    /// WriteAsync сначала завершает запись изменений,
    /// оставшихся после предыдущего сбоя.
    /// </remarks>
    internal async Task<string[]> GetDueSlugsAsync(
        CancellationToken cancellationToken)
    {
        return await database.WriteAsync(
            db => db.ScannedContracts
                .Where(entity => entity.Pending)
                .Where(entity =>
                    db.ContractRetries.Find(entity.Slug) is not { } retry ||
                    retry.Revision != entity.Revision ||
                    retry.NextAttemptAt is { } due && due <= clock.GetUtcNow())
                .OrderBy(entity => entity.ScannedAt)
                .ThenBy(entity => entity.Slug, StringComparer.Ordinal)
                .Take(options.Value.BatchSize)
                .Select(entity => entity.Slug)
                .ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Подготавливает одну попытку и возвращает её контекст.
    /// </summary>
    /// <remarks>
    /// Создаёт retry-запись при первой попытке или смене редакции.
    /// Возвращает null, если операция завершена, остановлена
    /// либо время следующей попытки ещё не наступило.
    /// </remarks>
    internal async Task<ContractDeliveryContext?> PrepareAsync(string slug, CancellationToken cancellationToken)
    {
        return await database.WriteAsync<ContractDeliveryContext?>(db =>
            {
                var entity = db.ScannedContracts.Find(slug);

                if (entity is null || !entity.Pending)
                    return null;

                var retry = db.ContractRetries.Find(slug);

                if (retry is null || retry.Revision != entity.Revision)
                {
                    db.ContractRetries.Remove(slug);

                    retry = new ContractRetryEntity
                    {
                        Slug = slug,
                        Revision = entity.Revision,
                        Stage = entity.TargetVersion.HasValue ? 
                            ScanningStage.Publication : ScanningStage.Synchronization,
                        NextAttemptAt = clock.GetUtcNow()
                    };

                    db.ContractRetries.Add(retry);
                }

                if (retry.NextAttemptAt is not { } due || due > clock.GetUtcNow())
                    return null;

                return new ContractDeliveryContext
                {
                    Slug = slug,
                    Stage = retry.Stage,
                    Version = entity.TargetVersion,
                    Rules = entity.Rules
                        .Select(rule => new ContractRule
                        {
                            FieldName = rule.FieldName,
                            Type = rule.Type,
                            Required = rule.Required,
                            Nullable = rule.Nullable
                        })
                        .ToArray()
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Сохраняет назначенную версию и следующий этап до отправки.
    /// </summary>
    internal Task SaveVersionAsync(
        ContractDeliveryContext context, int expectedVersion, ScanningStage next, CancellationToken cancellationToken) =>
        database.WriteAsync(db =>
            {
                var entity = db.ScannedContracts.Find(context.Slug)!;

                entity.ExpectedVersion = expectedVersion;
                entity.TargetVersion = context.Version;

                Advance(db.ContractRetries.Find(context.Slug)!, next);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Сохраняет подтверждение публикации со стороны Core.
    /// </summary>
    /// <remarks>
    /// После этого операция продолжится с локального сохранения
    /// без повторного HTTP-запроса.
    /// </remarks>
    internal Task ConfirmPublicationAsync(string slug, CancellationToken cancellationToken) =>
        database.WriteAsync(db =>
            {
                Advance(db.ContractRetries.Find(slug)!, ScanningStage.Persistence);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Подтверждает завершение доставки и удаляет retry-запись.
    /// </summary>
    /// <remarks>
    /// Pending=false сохраняется раньше удаления retry-записи.
    /// Если удаление завершится ошибкой, CleanupAsync завершит очистку.
    /// </remarks>
    internal async Task CompleteAsync(string slug, CancellationToken cancellationToken)
    {
        await database.WriteAsync(
            db =>
            {
                db.ScannedContracts.Find(slug)!.Pending = false;

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        await database.WriteAsync(
            db => db.ContractRetries.Remove(slug),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Сохраняет ошибку текущего этапа и расписание повтора.
    /// </summary>
    internal async Task FailAsync(
        ContractDeliveryContext context, Exception exception, CancellationToken cancellationToken)
    {
        await database.WriteAsync(
            db =>
            {
                var retry = db.ContractRetries.Find(context.Slug);

                if (retry is null)
                    return false;

                retry.Stage = context.Stage;
                retry.FailedAttempts = checked(retry.FailedAttempts + 1);
                retry.LastAttemptAt = clock.GetUtcNow();
                retry.LastError = exception.Message;
                retry.NextAttemptAt = policy.GetNextAttemptAt(
                    exception, retry.FailedAttempts, clock.GetUtcNow());

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Удаляет завершённые операции и записи предыдущих редакций.
    /// </summary>
    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await database.WriteAsync(
            db =>
            {
                var obsolete = db.ContractRetries
                    .Where(retry =>
                        db.ScannedContracts.Find(retry.Slug) is not { Pending: true } entity ||
                        entity.Revision != retry.Revision)
                    .Select(retry => retry.Slug)
                    .ToArray();

                foreach (var slug in obsolete)
                    db.ContractRetries.Remove(slug);

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Переводит операцию на следующий этап.
    /// </summary>
    /// <remarks>
    /// Повторная запись того же этапа не сбрасывает счётчик неудач.
    /// </remarks>
    private void Advance(ContractRetryEntity retry, ScanningStage stage)
    {
        if (retry.Stage != stage)
        {
            retry.Stage = stage;
            retry.FailedAttempts = 0;
            retry.LastError = null;
            retry.LastAttemptAt = null;
        }

        retry.NextAttemptAt = clock.GetUtcNow();
    }
}
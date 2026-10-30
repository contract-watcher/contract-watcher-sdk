using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Comparers;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Services;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Сохраняет правила приложения и решения о публикации после mapping.
/// </summary>
/// <remarks>
/// <para>
/// Не заменяет опубликованные контракты неподтверждёнными кандидатами.
/// </para>
/// <para>
/// Повтор записи сохраняет прогресс доставки той же редакции.
/// Общей транзакции между файлами MicroDb нет.
/// </para>
/// </remarks>
internal sealed class ContractPersistenceModule(
    IContractStore store, DatabaseAccess database, TimeProvider clock, ContractDeliveryGate? gate = null) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Persistence;

    public async Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (gate is null)
        {
            await PersistAsync(context, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await gate.Semaphore.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await PersistAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    /// <summary>
    /// Сохраняет результаты mapping и согласует существующие retry-записи.
    /// </summary>
    private async Task PersistAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        context.PersistenceCompleted = false;

        var results = context.MappingResults
            ?? throw new InvalidOperationException("Contract mapping has not completed.");

        // Подготавливаем независимые сущности до изменения базы.
        var incoming = results
            .Select(result =>
            {
                if (string.IsNullOrWhiteSpace(result.Slug))
                    throw new InvalidOperationException("Contract slug is required.");

                var entity = new ScannedContractEntity
                {
                    Slug = result.Slug,
                    Revision = Guid.NewGuid(),
                    ExpectedVersion = result.ExpectedVersion,
                    TargetVersion = result.TargetVersion,
                    Pending = result.Pending,
                    ScannedAt = clock.GetUtcNow()
                };

                foreach (var rule in result.Rules)
                {
                    entity.Rules.Add(new ContractRuleEntity
                    {
                        FieldName = rule.FieldName,
                        Type = rule.Type,
                        Required = rule.Required,
                        Nullable = rule.Nullable
                    });
                }

                return entity;
            })
            .ToDictionary(entity => entity.Slug, StringComparer.Ordinal);

        var knownSlugs = database.Read(db => db.ScannedContracts
            .Select(entity => entity.Slug)
            .ToArray());

        var removed = knownSlugs
            .Except(incoming.Keys, StringComparer.Ordinal)
            .ToArray();

        // Сначала удаляем локальные копии, затем записи учёта.
        // При сбое удаление можно безопасно повторить.
        await store.ApplyAsync([], removed, cancellationToken)
            .ConfigureAwait(false);

        await database.WriteAsync(
            db =>
            {
                foreach (var slug in removed)
                {
                    db.ScannedContracts.Remove(slug);
                    db.ContractRetries.Remove(slug);
                }

                foreach (var source in incoming.Values)
                {
                    var target = db.ScannedContracts.Find(source.Slug);

                    if (target is null)
                    {
                        db.ScannedContracts.Add(source);
                        continue;
                    }

                    var unchanged = ContractContentComparer.AreEqual(
                        ToContent(target),
                        ToContent(source));

                    var retry = db.ContractRetries.Find(source.Slug);

                    var keepCheckpoint =
                        unchanged &&
                        target.Pending &&
                        source.Pending &&
                        retry is not null &&
                        retry.Revision == target.Revision;

                    // Изменившиеся правила начинают отдельную операцию.
                    // Старые записи без Revision получают новый идентификатор.
                    if (!unchanged ||
                        target.Pending != source.Pending ||
                        target.Revision == Guid.Empty)
                    {
                        target.Revision = source.Revision;
                        db.ContractRetries.Remove(source.Slug);
                    }

                    // Core мог подтвердить POST перед падением процесса.
                    // Если новый startup scan не смог обратиться к Core,
                    // сохраняем уже подтверждённую версию и этап Persistence.
                    if (keepCheckpoint && retry!.Stage == ScanningStage.Persistence)
                    {
                        source.ExpectedVersion = target.ExpectedVersion;
                        source.TargetVersion = target.TargetVersion;
                    }

                    if (!source.Pending)
                        db.ContractRetries.Remove(source.Slug);

                    target.ExpectedVersion = source.ExpectedVersion;
                    target.TargetVersion = source.TargetVersion;
                    target.Pending = source.Pending;
                    target.ScannedAt = source.ScannedAt;
                    target.Rules.Clear();

                    foreach (var rule in source.Rules)
                        target.Rules.Add(rule);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        context.PersistenceCompleted = true;
    }

    /// <summary>
    /// Создаёт независимое представление для сравнения содержимого.
    /// </summary>
    /// <remarks>
    /// ContractContentComparer сравнивает правила без учёта версии.
    /// </remarks>
    private static PublishedContract ToContent(ScannedContractEntity entity) => new()
    {
        Slug = entity.Slug,
        ContractVersion = entity.TargetVersion ?? 1,
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
}
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Services;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Сохраняет правила приложения
/// и решения о публикации после mapping.
/// </summary>
/// <remarks>
/// Не заменяет опубликованные контракты
/// неподтверждёнными кандидатами.
/// Повтор записи идемпотентен.
/// Общей транзакции между файлами MicroDb нет.
/// </remarks>
internal sealed class ContractPersistenceModule(
    IContractStore store, DatabaseAccess database, TimeProvider clock) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Persistence;

    public async Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

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

        // Удаляем только контракты ранее обнаруженных endpoints.
        // Сначала локальные копии, затем записи учёта:
        // при падении удаление можно безопасно повторить.
        await store
            .ApplyAsync([], removed, cancellationToken)
            .ConfigureAwait(false);

        await database.WriteAsync(db =>
        {
            foreach (var slug in removed)
                db.ScannedContracts.Remove(slug);

            foreach (var source in incoming.Values)
            {
                var target = db.ScannedContracts.Find(source.Slug);

                if (target is null)
                {
                    db.ScannedContracts.Add(source);
                    continue;
                }

                target.ExpectedVersion = source.ExpectedVersion;
                target.TargetVersion = source.TargetVersion;
                target.Pending = source.Pending;
                target.ScannedAt = source.ScannedAt;
                target.Rules.Clear();

                foreach (var rule in source.Rules)
                    target.Rules.Add(rule);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
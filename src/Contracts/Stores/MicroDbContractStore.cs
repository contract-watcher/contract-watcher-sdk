using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Data.Mappings;
using ContractWatcher.SDK.Data.Services;

namespace ContractWatcher.SDK.Contracts.Stores;

/// <summary>
/// Хранит опубликованные контракты в MicroDb
/// и возвращает независимые DTO.
/// </summary>
internal sealed class MicroDbContractStore(DatabaseAccess database, TimeProvider clock) : IContractStore
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSlugs() =>
        database.Read(db => db.Contracts
            .Select(entity => entity.Slug)
            .ToArray());
    
    public PublishedContract? Get(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        return database.Read(db => db.Contracts.Find(slug)?.ToDto());
    }

    public async Task SetAsync(PublishedContract contract, CancellationToken cancellationToken = default)
    {
        if (contract is null)
            throw new ArgumentNullException(nameof(contract));

        if (string.IsNullOrWhiteSpace(contract.Slug))
            throw new ArgumentException("Contract Slug is required", nameof(contract));

        // Отделяем входные коллекции от вызывающего кода
        // до ожидания блокировки.
        var incoming = contract.ToEntity(clock.GetUtcNow());

        await database.WriteAsync(db =>
        {
            var existing = db.Contracts.Find(incoming.Slug);

            if (existing is null)
                db.Contracts.Add(incoming);
            else
                existing.Apply(incoming.ToDto(), incoming.UpdatedAt);

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
    
    public Task<bool> RemoveAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        return database.WriteAsync(db => db.Contracts.Remove(slug), cancellationToken);
    }
    
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await database.WriteAsync(db =>
        {
            var slugs = db.Contracts
                .Select(entity => entity.Slug)
                .ToArray();

            foreach (var slug in slugs)
                db.Contracts.Remove(slug);

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Подготавливает независимые сущности
    /// и применяет пакет под общей блокировкой.
    /// </summary>
    /// <remarks>
    /// Подтверждённое отсутствие в Core удаляет локальную копию.
    /// Существующие сущности обновляются через событийный трекинг.
    /// </remarks>
    public async Task ApplyAsync(
        IReadOnlyCollection<PublishedContract> contracts,
        IReadOnlyCollection<string> removedSlugs,
        CancellationToken cancellationToken = default)
    {
        if (contracts is null)
            throw new ArgumentNullException(nameof(contracts));
        
        if (removedSlugs is null)
            throw new ArgumentNullException(nameof(removedSlugs));

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var now = clock.GetUtcNow();

        var incoming = contracts
            .Select(contract => contract.ToEntity(now))
            .ToArray();

        var removed = removedSlugs.ToArray();

        if (incoming.Any(contract => string.IsNullOrWhiteSpace(contract.Slug)) ||
            removed.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Contract slug is required.");

        if (incoming.Select(contract => contract.Slug)
            .Distinct(StringComparer.Ordinal)
            .Count() != incoming.Length)
            throw new ArgumentException("Duplicate contract slug.", nameof(contracts));

        await database.WriteAsync(db =>
        {
            foreach (var slug in removed)
                db.Contracts.Remove(slug);

            foreach (var contract in incoming)
            {
                var existing = db.Contracts.Find(contract.Slug);

                if (existing is null)
                    db.Contracts.Add(contract);
                else
                    existing.Apply(contract.ToDto(), now);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
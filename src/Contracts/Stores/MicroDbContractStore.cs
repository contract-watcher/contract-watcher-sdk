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
}
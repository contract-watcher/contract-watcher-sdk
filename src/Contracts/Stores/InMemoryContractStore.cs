using System.Collections.Concurrent;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;

namespace ContractWatcher.SDK.Contracts.Stores;

/// <summary>
/// Реализует потокобезопасное in-memory хранилище опубликованных контрактов
/// </summary>
internal sealed class InMemoryContractStore : IContractStore
{
    /// <summary>
    /// Опубликованные контракты
    /// </summary>
    private readonly ConcurrentDictionary<string, PublishedContract> _contracts = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> GetSlugs() => _contracts.Keys.ToArray();

    public PublishedContract? Get(string slug)
    {
        if (String.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        return _contracts.GetValueOrDefault(slug);
    }

    public void Set(PublishedContract contract)
    {
        if (contract is null)
            throw new ArgumentNullException(nameof(contract));

        if (String.IsNullOrWhiteSpace(contract.Slug))
            throw new ArgumentException("Contract Slug is required", nameof(contract));

        _contracts[contract.Slug] = contract;
    }
    
    public bool Remove(string slug)
    {
        if (String.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        return _contracts.TryRemove(slug, out _);
    }
    
    public void Clear() => _contracts.Clear();
    
    public Task SetAsync(PublishedContract contract, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        Set(contract);

        return Task.CompletedTask;
    }
    
    public Task<bool> RemoveAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(Remove(slug));
    }
    
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        Clear();

        return Task.CompletedTask;
    }
}
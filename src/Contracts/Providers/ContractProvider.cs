using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;

namespace ContractWatcher.SDK.Contracts.Providers;

/// <summary>
/// Предоставляет опубликованные контракты из локального хранилища
/// </summary>
public sealed class ContractProvider : IContractProvider
{
    /// <summary>
    /// Хранилище полученных контрактов
    /// </summary>
    private readonly IContractStore _contractStore;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ContractProvider"/>
    /// </summary>
    /// <param name="contractStore">Локальное хранилище опубликованных контрактов</param>
    public ContractProvider(IContractStore contractStore) =>
        _contractStore = contractStore ?? throw new ArgumentNullException(nameof(contractStore));
    
    public Task<PublishedContract?> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(slug))
            throw new ArgumentException(nameof(slug));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var contract = _contractStore.Get(slug);

        return Task.FromResult(contract);
    }
}
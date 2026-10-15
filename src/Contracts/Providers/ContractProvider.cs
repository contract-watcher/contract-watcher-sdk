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
    
    private readonly IContractRefresher _contractRefresher;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ContractProvider"/>
    /// </summary>
    /// <param name="contractStore">Локальное хранилище опубликованных контрактов</param>
    /// <param name="contractRefresher">Сервис обновления контрактов</param>
    public ContractProvider(IContractStore contractStore, IContractRefresher contractRefresher)
    {
        _contractStore = contractStore ?? throw new ArgumentNullException(nameof(contractStore));
        _contractRefresher = contractRefresher ?? throw new ArgumentNullException(nameof(contractRefresher));
    }
    
    public Task<PublishedContract?> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(slug))
            throw new ArgumentException(nameof(slug));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var contract = _contractStore.Get(slug);
        
        if (contract is not null)
            return Task.FromResult<PublishedContract?>(contract);

        return _contractRefresher.RefreshAsync(slug, cancellationToken);
    }
}
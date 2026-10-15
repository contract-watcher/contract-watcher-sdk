using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Core.Interfaces;

namespace ContractWatcher.SDK.Contracts.Refreshers;

/// <summary>
/// Загружает актуальные контракты из Core и обновляет локальное хранилище
/// </summary>
public sealed class ContractRefresher(IContractStore contractStore, IContractWatcherCoreClient coreClient) : IContractRefresher
{
    public async Task<PublishedContract?> RefreshAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var contract = await coreClient.GetContractAsync(slug, cancellationToken);

        if (contract is null)
            return null;

        contractStore.Set(contract);

        return contract;
    }
}
using System.Diagnostics.CodeAnalysis;
using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Core.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Contracts.Workers;

public sealed class ContractRefreshWorker(
    IContractStore contractStore,
    IContractRefresher contractRefresher,
    IOptions<ContractWatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<ContractRefreshWorker> logger) : BackgroundService
{
    [ExcludeFromCodeCoverage]
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.Value.ContractRefreshInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshKnownContractsAsync(cancellationToken);
        }
    }

    public async Task RefreshKnownContractsAsync(CancellationToken cancellationToken)
    {
        var slugs = contractStore.GetSlugs();

        foreach (var slug in slugs)
        {
            try
            {
                await contractRefresher.RefreshAsync(slug, cancellationToken);
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (CoreRequestException exception)
            {
                logger.LogWarning(exception, "Failed to refresh contract '{ContractSlug}'.", slug);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error occurred while refreshing contract '{ContractSlug}'.", slug);
            }
        }
    }
}
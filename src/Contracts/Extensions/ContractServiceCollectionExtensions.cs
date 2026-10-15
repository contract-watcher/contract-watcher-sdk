using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Providers;
using ContractWatcher.SDK.Contracts.Refreshers;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Contracts.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Contracts.Extensions;

public static class ContractServiceCollectionExtensions
{
    public static IServiceCollection AddContractWatcherContracts(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IContractStore, InMemoryContractStore>();
        services.TryAddTransient<IContractRefresher, ContractRefresher>();
        services.TryAddTransient<IContractProvider, ContractProvider>();
        services.AddHostedService<ContractRefreshWorker>();

        return services;
    }
}
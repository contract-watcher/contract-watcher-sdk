using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Constants;
using ContractWatcher.SDK.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Core.Extensions;

/// <summary>
/// Содержит методы регистрации клиента ContractWatcher Core
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddContractWatcherCore(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddHttpClient<IContractWatcherCoreClient, ContractWatcherCoreClient>(
            static (serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ContractWatcherOptions>>().Value;

                httpClient.BaseAddress = new Uri(options.CoreUrl);
                httpClient.DefaultRequestHeaders.Add(CoreHeaders.ApiKey, options.IntegrationKey);
            });

        return services;
    }
}
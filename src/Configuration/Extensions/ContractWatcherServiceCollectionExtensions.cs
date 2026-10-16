using ContractWatcher.SDK.Configuration.Builders;
using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Configuration.Extensions;

/// <summary>
/// Содержит методы регистрации SDK
/// </summary>
public static class ContractWatcherServiceCollectionExtensions
{
    /// <summary>
    /// Начинает конфигурацию SDK
    /// </summary>
    public static ContractWatcherBuilder AddContractWatcher(
        this IServiceCollection services, Action<ContractWatcherOptions>? configure = null)
    {
        if (services is  null)
            throw new ArgumentNullException(nameof(services));

        var options = services.AddOptions<ContractWatcherOptions>();

        if (configure is not null)
            options.Configure(configure);

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<IContractWatcher, Watcher.ContractWatcher>();

        return new ContractWatcherBuilder(services);
    }
}
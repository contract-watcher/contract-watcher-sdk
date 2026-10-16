using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Configuration.Builders;

/// <summary>
/// Корневой builder конфигурации SDK
/// </summary>
public sealed class ContractWatcherBuilder
{
    public IServiceCollection Services { get; }

    internal ContractWatcherBuilder(IServiceCollection services) =>
        Services = services ?? throw new ArgumentNullException(nameof(services));
}
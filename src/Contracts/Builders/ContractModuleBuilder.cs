using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Providers;
using ContractWatcher.SDK.Contracts.Refreshers;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Contracts.Workers;
using ContractWatcher.SDK.Data.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Contracts.Builders;

/// <summary>
/// Настройка источников и хранения контрактов
/// </summary>
public sealed class ContractModuleBuilder
{
    public IServiceCollection Services { get; }

    internal ContractModuleBuilder(IServiceCollection services) =>
        Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Использует стандартное постоянное хранилище контрактов на MicroDb.
    /// </summary>
    public ContractModuleBuilder UseMicroDbStore()
    {
        Services.AddContractWatcherPersistence();

        return UseStore<MicroDbContractStore>();
    }

    /// <summary>
    /// Использует стандартный provider контрактов
    /// </summary>
    public ContractModuleBuilder UseDefaultProvider() => UseProvider<ContractProvider>();

    /// <summary>
    /// Использует стандартный механизм обновления контрактов через Core
    /// </summary>
    public ContractModuleBuilder UseCoreRefresher() => UseRefresher<ContractRefresher>();

    /// <summary>
    /// Использует пользовательское хранилище контрактов
    /// </summary>
    public ContractModuleBuilder UseStore<TStore>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TStore : class, IContractStore
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IContractStore), typeof(TStore), lifetime));

        return this;
    }

    /// <summary>
    /// Использует пользовательский механизм обновления контрактов
    /// </summary>
    public ContractModuleBuilder UseRefresher<TRefresher>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TRefresher : class, IContractRefresher
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IContractRefresher), typeof(TRefresher), lifetime));

        return this;
    }

    /// <summary>
    /// Использует пользовательский provider контрактов
    /// </summary>
    public ContractModuleBuilder UseProvider<TProvider>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TProvider : class, IContractProvider
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IContractProvider), typeof(TProvider), lifetime));

        return this;
    }

    /// <summary>
    /// Добавляет фоновое обновление контрактов
    /// </summary>
    public ContractModuleBuilder AddRefreshWorker()
    {
        Services
            .AddOptions<ContractWatcherOptions>()
            .Validate(static options => options.ContractRefreshInterval > TimeSpan.Zero,
                $"{nameof(ContractWatcherOptions.ContractRefreshInterval)} must be greater than zero.")
            .ValidateOnStart();

        Services.AddHostedService<ContractRefreshWorker>();

        return this;
    }
}
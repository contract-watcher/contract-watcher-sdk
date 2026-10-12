using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Providers;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Execution.Extensions;
using ContractWatcher.SDK.Reporting.Extensions;
using ContractWatcher.SDK.Validation.Extensions;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Watcher.Extensions;

/// <summary>
/// Содержит методы регистрации обработки контрактов в DI-контейнере
/// </summary>
public static class WatcherServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет сервисы обработки контрактов в DI-контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Возвращает исходную коллекцию сервисов</returns>
    public static IServiceCollection AddContractWatcher(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<IContractStore, InMemoryContractStore>();
        services.TryAddTransient<IContractProvider, ContractProvider>();
        services.AddContractValidation();
        services.AddContractWatcherReporting();
        services.AddContractWatcherExecution();
        services.TryAddTransient<IContractWatcher, ContractWatcher>();

        return services;
    }
}
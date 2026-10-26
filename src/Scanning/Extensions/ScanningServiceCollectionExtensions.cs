using ContractWatcher.SDK.Scanning.Discovery;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Mappings;
using ContractWatcher.SDK.Scanning.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Scanning.Extensions;

/// <summary>
/// Регистрирует компоненты автоматического сбора MVC-контрактов.
/// </summary>
internal static class ScanningServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет discovery, генератор схем, mapper и scanner.
    /// </summary>
    /// <remarks>
    /// Предполагает, что пользовательское приложение зарегистрировало MVC.
    /// Повторный вызов не дублирует регистрации.
    /// </remarks>
    internal static IServiceCollection AddContractWatcherScanning(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<IEndpointDiscovery, EndpointDiscovery>();
        services.TryAddSingleton<IJsonSchemaGenerator, JsonSchemaGenerator>();
        services.TryAddSingleton<IContractMapper, ContractMapper>();
        services.TryAddSingleton<IContractScanner, ContractScanner>();

        return services;
    }
}
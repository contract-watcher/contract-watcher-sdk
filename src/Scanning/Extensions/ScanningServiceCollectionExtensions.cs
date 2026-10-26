using ContractWatcher.SDK.Scanning.Builders;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Options;
using ContractWatcher.SDK.Scanning.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Scanning.Extensions;

/// <summary>
/// Регистрирует исполнитель, модули и однократный startup worker.
/// </summary>
internal static class ScanningServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет стандартную цепочку и применяет пользовательские настройки.
    /// </summary>
    internal static IServiceCollection AddContractWatcherScanning(this IServiceCollection services, Action<ScanningModuleBuilder>? configure = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddOptions<ScanningRetryOptions>()
            .Validate(value => 
                value.MaxAttempts > 0, "MaxAttempts must be positive.")
            .Validate(value => 
                value.Delay >= TimeSpan.Zero && value.Delay <= TimeSpan.FromDays(1), "Delay must be within [0, 1 day].")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IContractScanner, ContractScanner>();

        var builder = new ScanningModuleBuilder(services).UseDefaultModules();

        configure?.Invoke(builder);

        services.AddHostedService<ContractScanningWorker>();

        return services;
    }
}
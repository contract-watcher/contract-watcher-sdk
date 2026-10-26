using ContractWatcher.SDK.Scanning.Builders;
using ContractWatcher.SDK.Scanning.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.AspNetCore.Extensions;

/// <summary>
/// Подключает автоматический сбор MVC-контрактов при старте приложения.
/// </summary>
public static class ContractWatcherScanningMvcExtensions
{
    /// <summary>
    /// Добавляет стандартные модули и позволяет изменить их состав или настройки.
    /// </summary>
    public static IMvcBuilder AddContractWatcherScanning(
        this IMvcBuilder builder, Action<ScanningModuleBuilder>? configure = null)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.AddContractWatcherScanning(configure);

        return builder;
    }
}
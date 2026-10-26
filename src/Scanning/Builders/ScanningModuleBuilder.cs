using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Scanning.Builders;

/// <summary>
/// Настраивает состав, порядок и повторные попытки модулей сканирования.
/// </summary>
public sealed class ScanningModuleBuilder
{
    /// <summary>
    /// Коллекция сервисов приложения.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Создаёт builder для текущего контейнера.
    /// </summary>
    internal ScanningModuleBuilder(IServiceCollection services) =>
        Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Добавляет модуль в конец цепочки без дублирования его типа.
    /// </summary>
    public ScanningModuleBuilder AddModule<TModule>() where TModule : class, IScanningModule
    {
        Services.TryAddEnumerable(ServiceDescriptor.Transient<IScanningModule, TModule>());

        return this;
    }

    /// <summary>
    /// Заменяет зарегистрированный модуль, сохраняя его позицию.
    /// </summary>
    /// <remarks>
    /// Работает с регистрациями по типу, создаваемыми AddModule.
    /// </remarks>
    public ScanningModuleBuilder ReplaceModule<TCurrent, TReplacement>()
        where TCurrent : class, IScanningModule
        where TReplacement : class, IScanningModule
    {
        for (var index = 0; index < Services.Count; index++)
        {
            var descriptor = Services[index];

            if (descriptor.ServiceType != typeof(IScanningModule) ||
                descriptor.ImplementationType != typeof(TCurrent))
                continue;

            Services[index] = ServiceDescriptor.Transient<IScanningModule, TReplacement>();

            return this;
        }

        throw new InvalidOperationException($"Module '{typeof(TCurrent).Name}' is not registered.");
    }

    /// <summary>
    /// Удаляет все модули для сборки собственной последовательности.
    /// </summary>
    public ScanningModuleBuilder ClearModules()
    {
        Services.RemoveAll<IScanningModule>();

        return this;
    }

    /// <summary>
    /// Добавляет поиск endpoints.
    /// </summary>
    public ScanningModuleBuilder AddDiscovery() => AddModule<EndpointDiscoveryModule>();

    /// <summary>
    /// Добавляет генерацию схем.
    /// </summary>
    public ScanningModuleBuilder AddSchemaGeneration() => AddModule<SchemaGenerationModule>();

    /// <summary>
    /// Добавляет формирование контрактов.
    /// </summary>
    public ScanningModuleBuilder AddMapping() => AddModule<ContractMappingModule>();

    /// <summary>
    /// Добавляет стандартные этапы в порядке их зависимостей.
    /// </summary>
    public ScanningModuleBuilder UseDefaultModules() =>
        AddDiscovery()
        .AddSchemaGeneration()
        .AddMapping();

    /// <summary>
    /// Настраивает количество попыток и задержку между ними.
    /// </summary>
    public ScanningModuleBuilder ConfigureRetry(Action<ScanningRetryOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);

        return this;
    }
}
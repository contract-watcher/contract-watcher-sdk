using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Publishers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Reporting.Extensions;

/// <summary>
/// Содержит методы регистрации компонентов формирования отчетов
/// </summary>
public static class ReportingServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет компоненты формирования отчетов проверки контрактов
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения</param>
    /// <returns>Коллекция сервисов приложения</returns>
    public static IServiceCollection AddContractWatcherReporting(this IServiceCollection services)
    {
        if (services is  null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Transient<IReportBuildHandler, ReportMetadataHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IReportBuildHandler, ReportStatusHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IReportBuildHandler, ReportViolationsHandler>());
        services.TryAddTransient<IValidationReportBuilder, ValidationReportBuilder>();
        services.TryAddScoped<IReportPublisher, CoreReportPublisher>();

        return services;
    }
}
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Publishers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Reporting.Builders;

/// <summary>
/// Настройка формирования и публикации отчетов валидации
/// </summary>
public sealed class ReportingModuleBuilder
{
    public IServiceCollection Services { get; }

    internal ReportingModuleBuilder(IServiceCollection services) =>
        Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Добавляет стандартный builder отчетов валидации
    /// </summary>
    public ReportingModuleBuilder AddDefaultReportBuilder()
    {
        AddBuildHandler<ReportMetadataHandler>();
        AddBuildHandler<ReportStatusHandler>();
        AddBuildHandler<ReportViolationsHandler>();

        UseReportBuilder<ValidationReportBuilder>();

        return this;
    }

    /// <summary>
    /// Добавляет обработчик формирования отчета
    /// </summary>
    public ReportingModuleBuilder AddBuildHandler<THandler>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where THandler : class, IReportBuildHandler
    {
        Services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IReportBuildHandler), typeof(THandler), lifetime));

        return this;
    }

    /// <summary>
    /// Использует пользовательский builder отчетов
    /// </summary>
    public ReportingModuleBuilder UseReportBuilder<TBuilder>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TBuilder : class, IValidationReportBuilder
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IValidationReportBuilder), typeof(TBuilder), lifetime));

        return this;
    }

    /// <summary>
    /// Публикует отчеты в Core
    /// </summary>
    public ReportingModuleBuilder UseCorePublisher() => UsePublisher<CoreReportPublisher>();

    /// <summary>
    /// Использует пользовательский publisher отчетов
    /// </summary>
    public ReportingModuleBuilder UsePublisher<TPublisher>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TPublisher : class, IReportPublisher
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IReportPublisher), typeof(TPublisher), lifetime));

        return this;
    }
}
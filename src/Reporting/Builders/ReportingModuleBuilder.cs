using ContractWatcher.SDK.Data.Extensions;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Options;
using ContractWatcher.SDK.Reporting.Publishers;
using ContractWatcher.SDK.Reporting.Services;
using ContractWatcher.SDK.Reporting.Workers;
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
    /// Сохраняет отчёты в MicroDb, отправляет в Core и добавляет фоновую повторную отправку.
    /// </summary>
    /// <param name="configureRetry">Необязательная настройка расписания и лимита попыток.</param>
    public ReportingModuleBuilder UseCorePublisher(Action<ReportRetryOptions>? configureRetry = null)
    {
        Services.AddContractWatcherPersistence();

        var options = Services.AddOptions<ReportRetryOptions>();

        if (configureRetry is not null)
            options.Configure(configureRetry);

        options
            .Validate(value =>
                    value.PollInterval > TimeSpan.Zero &&
                    value.PollInterval <= TimeSpan.FromDays(1),
                "PollInterval must be within (0, 1 day].")
            .Validate(value =>
                    value.InitialDelay > TimeSpan.Zero &&
                    value.MaxDelay >= value.InitialDelay &&
                    value.MaxDelay <= TimeSpan.FromDays(365),
                "Invalid retry delay range.")
            .Validate(value => 
                    value.MaxAttempts >= 0 && value.BatchSize > 0, "Invalid retry limits.")
            .ValidateOnStart();

        Services.TryAddSingleton<ReportDeliveryService>();
        Services.AddHostedService<ReportRetryWorker>();

        return UsePublisher<CoreReportPublisher>(ServiceLifetime.Singleton);
    }

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
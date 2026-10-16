using ContractWatcher.SDK.Configuration.Builders;
using ContractWatcher.SDK.Contracts.Builders;
using ContractWatcher.SDK.Core.Extensions;
using ContractWatcher.SDK.Execution.Builders;
using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Validation.Builders;

namespace ContractWatcher.SDK.Configuration.Extensions;

/// <summary>
/// Содержит методы подключения модулей SDK
/// </summary>
public static class ContractWatcherBuilderExtensions
{
    /// <summary>
    /// Добавляет HTTP-интеграцию с Core
    /// </summary>
    public static ContractWatcherBuilder AddCore(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.AddContractWatcherCore();

        return builder;
    }

    /// <summary>
    /// Добавляет модуль работы с контрактами
    /// </summary>
    public static ContractWatcherBuilder AddContracts(
        this ContractWatcherBuilder builder, Action<ContractModuleBuilder> configure)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new ContractModuleBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Добавляет стандартную конфигурацию контрактов, получаемых из ContractWatcher Core.
    /// </summary>
    public static ContractWatcherBuilder AddCoreContracts(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddContracts(contracts =>
        {
            contracts
                .UseInMemoryStore()
                .UseCoreRefresher()
                .UseDefaultProvider()
                .AddRefreshWorker();
        });
    }

    /// <summary>
    /// Добавляет модуль валидации контрактов
    /// </summary>
    public static ContractWatcherBuilder AddValidation(
        this ContractWatcherBuilder builder, Action<ValidationModuleBuilder> configure)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new ValidationModuleBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Добавляет стандартные правила валидации
    /// </summary>
    public static ContractWatcherBuilder AddDefaultValidation(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddValidation(validation => validation.AddDefaultRules());
    }

    /// <summary>
    /// Добавляет модуль формирования и публикации отчетов
    /// </summary>
    public static ContractWatcherBuilder AddReporting(
        this ContractWatcherBuilder builder, Action<ReportingModuleBuilder> configure)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new ReportingModuleBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Добавляет стандартный механизм формирования отчетов
    /// Способ публикации отчета настраивается отдельно
    /// </summary>
    public static ContractWatcherBuilder AddDefaultReporting(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddReporting(reporting => reporting.AddDefaultReportBuilder());
    }

    /// <summary>
    /// Добавляет execution pipeline
    /// </summary>
    public static ContractWatcherBuilder AddExecution(
        this ContractWatcherBuilder builder, Action<ExecutionModuleBuilder> configure)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new ExecutionModuleBuilder(builder.Services));

        return builder;
    }

    /// <summary>
    /// Добавляет стандартный execution pipeline
    /// </summary>
    public static ContractWatcherBuilder AddDefaultExecution(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.AddExecution(execution => execution.AddDefaultMiddlewares());
    }

    /// <summary>
    /// Добавляет стандартную конфигурацию SDK
    /// с получением контрактов и публикацией отчетов через Core
    /// </summary>
    public static ContractWatcherBuilder AddDefaults(this ContractWatcherBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder
            .AddCore()
            .AddCoreContracts()
            .AddDefaultValidation()
            .AddDefaultReporting()
            .AddReporting(reporting => reporting.UseCorePublisher())
            .AddDefaultExecution();
    }
}
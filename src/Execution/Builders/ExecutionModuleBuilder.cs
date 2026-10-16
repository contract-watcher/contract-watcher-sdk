using ContractWatcher.SDK.Execution.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Execution.Builders;

/// <summary>
/// Настройка execution pipeline
/// </summary>
public sealed class ExecutionModuleBuilder
{
    private readonly ExecutionPipelineBuilder _pipelineBuilder;

    public IServiceCollection Services { get; }

    internal ExecutionModuleBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));

        Services.TryAddTransient<IExecutionPipeline, ExecutionPipeline>();

        _pipelineBuilder = new ExecutionPipelineBuilder(Services);
    }

    /// <summary>
    /// Добавляет стандартный набор middleware
    /// </summary>
    public ExecutionModuleBuilder AddDefaultMiddlewares()
    {
        _pipelineBuilder.UseDefaultMiddlewares();

        return this;
    }

    /// <summary>
    /// Позволяет вручную настроить execution pipeline
    /// </summary>
    public ExecutionModuleBuilder Configure(Action<ExecutionPipelineBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(_pipelineBuilder);

        return this;
    }

    /// <summary>
    /// Использует пользовательскую реализацию execution pipeline
    /// </summary>
    public ExecutionModuleBuilder UsePipeline<TPipeline>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TPipeline : class, IExecutionPipeline
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IExecutionPipeline), typeof(TPipeline), lifetime));

        return this;
    }
}
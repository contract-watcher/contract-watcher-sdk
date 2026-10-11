using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Execution.Builders;

/// <summary>
/// Предоставляет API для настройки execution pipeline приложения
/// </summary>
public sealed class ExecutionPipelineBuilder
{
    private readonly IServiceCollection _services;

    public ExecutionPipelineBuilder(IServiceCollection services) => 
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Добавляет middleware в конец execution pipeline
    /// </summary>
    /// <typeparam name="TMiddleware">Тип добавляемого middleware</typeparam>
    /// <returns>Текущий экземпляр builder</returns>
    public ExecutionPipelineBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IWatcherMiddleware
    {
        _services.TryAddEnumerable(ServiceDescriptor.Transient<IWatcherMiddleware, TMiddleware>());

        return this;
    }

    /// <summary>
    /// Удаляет все зарегистрированные middleware
    /// </summary>
    public ExecutionPipelineBuilder ClearMiddlewares()
    {
        _services.RemoveAll<IWatcherMiddleware>();

        return this;
    }

    /// <summary>
    /// Добавляет стандартные middleware приложения
    /// </summary>
    public ExecutionPipelineBuilder UseDefaultMiddlewares()
    {
        return 
            AddMiddleware<ContractResolutionMiddleware>()
            .AddMiddleware<ValidationMiddleware>();
    }
}
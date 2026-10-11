using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Execution;

/// <summary>
/// Выполняет зарегистрированные middleware приложения в порядке их регистрации
/// </summary>
public sealed class ExecutionPipeline : IExecutionPipeline
{
    private readonly WatcherDelegate _pipeline;

    /// <summary>
    /// Инициализирует новый экземпляр execution pipeline
    /// </summary>
    /// <param name="middlewares">Последовательность middleware в порядке их выполнения</param>
    public ExecutionPipeline(IEnumerable<IWatcherMiddleware> middlewares)
    {
        if (middlewares is null)
            throw new ArgumentNullException(nameof(middlewares));

        _pipeline = BuildPipeline(middlewares.ToArray());
    }
    
    public Task ExecuteAsync(WatcherContext context, CancellationToken cancellationToken = default)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        return _pipeline(context, cancellationToken);
    }

    private static WatcherDelegate BuildPipeline(IReadOnlyList<IWatcherMiddleware> middlewares)
    {
        WatcherDelegate pipeline = static (_, _) => Task.CompletedTask;

        for (var index = middlewares.Count - 1; index >= 0; index--)
        {
            var middleware = middlewares[index];
            var next = pipeline;

            pipeline = (context, cancellationToken) => middleware.InvokeAsync(context, next, cancellationToken);
        }

        return pipeline;
    }
}
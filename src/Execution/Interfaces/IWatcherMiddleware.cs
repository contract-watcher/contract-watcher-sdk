using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Execution.Interfaces;

/// <summary>
/// Представляет отдельный этап pipeline выполнения обработки контрактов
/// </summary>
public interface IWatcherMiddleware
{
    /// <summary>
    /// Выполняет текущий этап pipeline
    /// </summary>
    Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken);
}
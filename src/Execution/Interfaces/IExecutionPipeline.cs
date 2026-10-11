using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Execution.Interfaces;

/// <summary>
/// Pipeline выполнения сценария обработки контрактов
/// </summary>
public interface IExecutionPipeline
{
    /// <summary>
    /// Выполняет настроенную последовательность middleware для указанного контекста
    /// </summary>
    /// <param name="context">Контекст выполнения ContractWatcher</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task ExecuteAsync(WatcherContext context, CancellationToken cancellationToken = default);
}
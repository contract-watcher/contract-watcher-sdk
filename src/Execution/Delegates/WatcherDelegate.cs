using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Execution.Delegates;

/// <summary>
/// Представляет следующий этап выполнения pipeline
/// </summary>
/// <param name="context">Контекст текущего выполнения приложения</param>
/// <param name="cancellationToken">Токен отмены операции</param>
/// <returns> Возвращает задачу, представляющую асинхронное выполнение следующего этапа pipeline</returns>
public delegate Task WatcherDelegate(WatcherContext context, CancellationToken cancellationToken);
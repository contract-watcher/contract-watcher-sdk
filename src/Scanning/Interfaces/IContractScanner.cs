using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Выполняет модули сканирования с повтором упавшего этапа.
/// </summary>
public interface IContractScanner
{
    /// <summary>
    /// Создаёт новый контекст и выполняет один проход пайплайна.
    /// </summary>
    /// <remarks>
    /// Однократный автоматический запуск обеспечивает startup worker.
    /// Явный повторный вызов этого метода запускает новый проход.
    /// </remarks>
    Task<IReadOnlyCollection<PublishedContract>> ScanAsync(CancellationToken cancellationToken = default);
}
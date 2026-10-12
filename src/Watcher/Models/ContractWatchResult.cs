using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Enums;

namespace ContractWatcher.SDK.Watcher.Models;

/// <summary>
/// Локальный результат выполнения проверки
/// </summary>
public sealed class ContractWatchResult
{
    /// <summary>
    /// Итоговый статус выполнения проверки
    /// </summary>
    public required WatcherStatus Status { get; init; }

    /// <summary>
    /// Нарушения, обнаруженные при проверке
    /// </summary>
    public required IReadOnlyCollection<ValidationViolation> Violations { get; init; }
}
namespace ContractWatcher.SDK.Execution.Enums;

/// <summary>
/// Определяет состояние выполнения проверки
/// </summary>
public enum WatcherStatus
{
    /// <summary>
    /// Выполнение pipeline ещё не завершено
    /// </summary>
    Pending,
    
    /// <summary>
    /// Проверка выполнена успешно, нарушений контракта не обнаружено
    /// </summary>
    Passed,
    
    /// <summary>
    /// Проверка выполнена, обнаружены нарушения контракта
    /// </summary>
    Failed,
    
    /// <summary>
    /// Проверка не была выполнена, поскольку требуемый контракт недоступен
    /// </summary>
    ContractUnavailable
}
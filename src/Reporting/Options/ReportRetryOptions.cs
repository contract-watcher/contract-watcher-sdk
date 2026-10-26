namespace ContractWatcher.SDK.Reporting.Options;

/// <summary>
/// Настраивает фоновую повторную отправку сохранённых отчётов.
/// </summary>
public sealed class ReportRetryOptions
{
    /// <summary>
    /// Период поиска отчётов, срок отправки которых наступил.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Начальная задержка после временной ошибки Core.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Верхняя граница экспоненциальной задержки,
    /// включая случайную добавку.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Максимум неудач с учётом первой отправки.
    /// Значение 0 означает отсутствие ограничения.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Максимальное число отчётов в одном проходе воркера.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
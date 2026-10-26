namespace ContractWatcher.SDK.Scanning.Options;

/// <summary>
/// Ограничивает повторные попытки каждого модуля сканирования.
/// </summary>
public sealed class ScanningRetryOptions
{
    /// <summary>
    /// Общее количество попыток, включая первоначальную.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Задержка перед следующей попыткой того же модуля.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);
}
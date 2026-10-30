namespace ContractWatcher.SDK.Contracts.Options;

/// <summary>
/// Настройки фоновой доставки контрактов и повторных попыток.
/// </summary>
public sealed class ContractPublicationOptions
{
    /// <summary>
    /// Максимальное число неудачных попыток одного этапа,
    /// включая первую попытку.
    /// </summary>
    /// <remarks>
    /// Значение 0 отключает ограничение.
    /// При переходе к следующему этапу счётчик сбрасывается.
    /// </remarks>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Начальная задержка перед повторной попыткой.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальная экспоненциальная задержка.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Интервал проверки сохранённой очереди.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальное количество контрактов в одном проходе воркера.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
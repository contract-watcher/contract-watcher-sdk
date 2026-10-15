namespace ContractWatcher.SDK.Configuration.Options;

/// <summary>
/// Настройки ContractWatcher
/// </summary>
public sealed class ContractWatcherOptions
{
    /// <summary>
    /// Адрес ContractWatcher Core
    /// </summary>
    public string CoreUrl { get; set; } = string.Empty;

    /// <summary>
    /// Ключ интеграции
    /// </summary>
    public string IntegrationKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Интервал фонового обновления опубликованных контрактов
    /// </summary>
    public TimeSpan ContractRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
}
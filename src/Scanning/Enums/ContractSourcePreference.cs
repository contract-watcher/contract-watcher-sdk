namespace ContractWatcher.SDK.Scanning.Enums;

/// <summary>
/// Определяет источник предлагаемых изменений контракта.
/// </summary>
public enum ContractSourcePreference
{
    /// <summary>
    /// Использовать опубликованное состояние.
    /// Результаты сканирования автоматически не публикуются.
    /// </summary>
    Published,

    /// <summary>
    /// Подготавливать публикацию отличий,
    /// обнаруженных при сканировании приложения.
    /// </summary>
    Application
}
using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Scanning.Options;

/// <summary>
/// Настройки однократной синхронизации результатов сканирования.
/// </summary>
public sealed class ContractSynchronizationOptions
{
    /// <summary>
    /// Источник изменений.
    /// По умолчанию автоматическая публикация отключена.
    /// </summary>
    public ContractSourcePreference SourcePreference { get; set; } = ContractSourcePreference.Published;
}
using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Execution.Models;

/// <summary>
/// Состояние выполнения проверки
/// </summary>
public sealed class WatcherContext
{
    /// <summary>
    /// Slug контракта, используемого для проверки
    /// </summary>
    public required string ContractSlug { get; init; }

    /// <summary>
    /// Входящий JSON для проверки
    /// </summary>
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// Контракт, разрешённый для текущей проверки
    /// </summary>
    public PublishedContract? Contract { get; set; }

    /// <summary>
    /// Результат локальной проверки
    /// </summary>
    public ContractValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Итоговый статус выполнения сценария
    /// </summary>
    public WatcherStatus Status { get; set; } = WatcherStatus.Pending;
}
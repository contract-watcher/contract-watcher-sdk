using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Reporting.Models;

/// <summary>
/// Содержит данные, используемые при формировании отчета проверки
/// </summary>
public sealed class ReportBuildContext
{
    /// <summary>
    /// Контракт, использованный при проверке
    /// </summary>
    public required PublishedContract Contract { get; init; }

    /// <summary>
    /// Результат проверки контракта
    /// </summary>
    public required ContractValidationResult ValidationResult { get; init; }

    /// <summary>
    /// ID формируемого отчета
    /// </summary>
    public Guid ReportId { get; set; }

    /// <summary>
    /// Время формирования отчета
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Статус проверки
    /// </summary>
    public ValidationStatus? Status { get; set; }

    /// <summary>
    /// Нарушения проверки контракта
    /// </summary>
    public IReadOnlyCollection<ValidationViolation> Violations { get; set; } = [];
}
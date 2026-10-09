using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Validation.Models;

/// <summary>
/// Представляет результат локальной проверки входящего JSON
/// </summary>
public class ContractValidationResult
{
    /// <summary>
    /// Получает признак успешного прохождения проверки
    /// </summary>
    public bool IsValid => Violations.Count == 0;

    /// <summary>
    /// Получает найденные нарушения контракта
    /// </summary>
    public required IReadOnlyCollection<ValidationViolation> Violations { get; init; }
}
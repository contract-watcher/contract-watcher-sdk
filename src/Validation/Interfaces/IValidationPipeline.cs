using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Interfaces;

/// <summary>
/// Представляет конвейер последовательного выполнения правил проверки одного поля входящего JSON
/// </summary>
public interface IValidationPipeline
{
    /// <summary>
    /// Выполняет все применимые правила проверки для указанного контекста
    /// </summary>
    /// <param name="context">Контекст проверяемого поля</param>
    /// <returns>Коллекция найденных нарушений</returns>
    IReadOnlyCollection<ValidationViolation> Validate(ValidationContext context);
}
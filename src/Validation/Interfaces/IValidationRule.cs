using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Interfaces;

/// <summary>
/// Представляет отдельное правило проверки поля входящего JSON
/// </summary>
public interface IValidationRule
{
    /// <summary>
    /// Определяет, применимо ли правило к указанному контексту
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Возвращает true, если правило должно быть выполнено; иначе false
    /// </returns>
    bool CanValidate(ValidationContext context);

    /// <summary>
    /// Выполняет проверку поля
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Найденное нарушение либо null, если правило не нарушено
    /// </returns>
    ValidationViolation? Validate(ValidationContext context);
}
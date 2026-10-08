using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Rules;

/// <summary>
/// Проверяет допустимость значения null для поля входящего JSON
/// </summary>
/// <remarks>
/// Правило применяется только к существующим полям
/// Отсутствие поля не является нарушением данного правила и проверяется отдельно правилом required
/// </remarks>
public class NullableRule : IValidationRule
{
    /// <summary>
    /// Определяет, применима ли проверка допустимости null к указанному полю
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Возвращает true, если поле присутствует во входящем JSON; иначе false
    /// </returns>
    public bool CanValidate(ValidationContext context) => context.Value is not null;

    /// <summary>
    /// Проверяет допустимость значения null для поля
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Возвращает <see cref="ViolationType.NullNotAllowed"/>, если поле содержит null, но контракт запрещает его; иначе null
    /// </returns>
    public ValidationViolation? Validate(ValidationContext context)
    {
        if (context.Value?.ValueKind != JsonValueKind.Null)
            return null;

        if (context.Rule.Nullable)
            return null;

        return new ValidationViolation
        {
            FieldName = context.Rule.FieldName,
            Type = ViolationType.NullNotAllowed,
            ExpectedType = context.Rule.Type,
            ActualType = null
        };
    }
}
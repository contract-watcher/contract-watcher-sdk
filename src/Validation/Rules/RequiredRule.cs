using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Rules;

/// <summary>
/// Проверяет наличие обязательного поля во входящем JSON
/// </summary>
/// <remarks>
/// Правило применяется только к полям, помеченным как обязательные
/// </remarks>
public class RequiredRule : IValidationRule
{
    /// <summary>
    /// Определяет, необходимо ли применять проверку обязательности к указанному полю
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Возвращает true, если поле является обязательным; иначе false/>
    /// </returns>
    public bool CanValidate(ValidationContext context) => context.Rule.Required;

    /// <summary>
    /// Проверяет наличие обязательного поля во входящем JSON
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>
    /// Нарушение <see cref="ViolationType.RequiredFieldMissing"/>, если обязательное поле отсутствует; иначе <see langword="null"/>
    /// </returns>
    public ValidationViolation? Validate(ValidationContext context)
    {
        if (!context.Rule.Required)
            return null;

        if (context.Value is not null)
            return null;

        return new ValidationViolation
        {
            FieldName = context.Rule.FieldName,
            Type = ViolationType.RequiredFieldMissing,
            ExpectedType = context.Rule.Type
        };
    }
}
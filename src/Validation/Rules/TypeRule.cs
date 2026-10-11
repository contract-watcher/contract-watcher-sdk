using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Rules;

/// <summary>
/// Проверяет соответствие типа значения поля типу, указанному в контракте
/// </summary>
/// <remarks>Правило применяется только к существующим полям, значение которых не равно null</remarks>
public class TypeRule : IValidationRule
{
    /// <summary>
    /// Определяет, применима ли проверка типа к указанному полю
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>Возвращает true, если поле существует и его значение не равно null; иначе false</returns>
    public bool CanValidate(ValidationContext context) => context.Value is { ValueKind: not JsonValueKind.Null };

    /// <summary>
    /// Проверяет соответствие фактического JSON-типа ожидаемому типу контракта
    /// </summary>
    /// <param name="context">Контекст проверки поля</param>
    /// <returns>Возвращает <see cref="ViolationType.TypeMismatch"/>, если типы не совпадают; иначе null</returns>
    public ValidationViolation? Validate(ValidationContext context)
    {
        var valueKind = context.Value!.Value.ValueKind;

        if (MatchesType(context.Rule.Type, valueKind))
            return null;

        return new ValidationViolation
        {
            FieldName = context.Rule.FieldName,
            Type = ViolationType.TypeMismatch,
            ExpectedType = context.Rule.Type,
            ActualType = GetContractFieldType(valueKind)
        };
    }

    /// <summary>
    /// Определяет, соответствует ли JSON-тип ожидаемому типу контракта
    /// </summary>
    private static bool MatchesType(ContractFieldType expectedType, JsonValueKind actualType) =>
        expectedType switch
        {
            ContractFieldType.String => actualType == JsonValueKind.String,
            ContractFieldType.Number => actualType == JsonValueKind.Number,
            ContractFieldType.Boolean => actualType is JsonValueKind.True or JsonValueKind.False,
            ContractFieldType.Object => actualType == JsonValueKind.Object,
            ContractFieldType.Array => actualType == JsonValueKind.Array,
            
            _ => false
        };

    /// <summary>
    /// Преобразует фактический JSON-тип в тип поля ContractWatcher
    /// </summary>
    private static ContractFieldType GetContractFieldType(JsonValueKind valueKind) =>
        valueKind switch
        {
            JsonValueKind.String => ContractFieldType.String,
            JsonValueKind.Number => ContractFieldType.Number,
            JsonValueKind.True or JsonValueKind.False => ContractFieldType.Boolean,
            JsonValueKind.Object => ContractFieldType.Object,
            JsonValueKind.Array => ContractFieldType.Array,

            _ => throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, "Неподдерживаемый JSON-тип")
        };
}
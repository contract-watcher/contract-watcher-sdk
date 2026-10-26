using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Data.Abstractions;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Представляет вложенное правило проверки контракта.
/// Хранится внутри файла контрактов.
/// </summary>
internal sealed class ContractRuleEntity : TrackedEntity
{
    /// <summary>
    /// Получает или задаёт имя проверяемого поля.
    /// </summary>
    public required string FieldName
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт ожидаемый тип поля.
    /// </summary>
    public required ContractFieldType Type
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт обязательность присутствия поля.
    /// </summary>
    public required bool Required
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт допустимость значения null.
    /// </summary>
    public required bool Nullable
    {
        get;
        set => SetValue(ref field, value);
    }
}
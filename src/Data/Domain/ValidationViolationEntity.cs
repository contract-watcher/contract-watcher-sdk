using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Data.Abstractions;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Представляет вложенное нарушение проверки.
/// Хранится вместе с отчётом.
/// </summary>
internal sealed class ValidationViolationEntity : TrackedEntity
{
    /// <summary>
    /// Получает или задаёт имя поля, содержащего нарушение.
    /// </summary>
    public required string FieldName
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт тип нарушения.
    /// </summary>
    public required ViolationType Type
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт ожидаемый тип поля,
    /// если он известен.
    /// </summary>
    public ContractFieldType? ExpectedType
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт фактический тип поля,
    /// если он известен.
    /// </summary>
    public ContractFieldType? ActualType
    {
        get;
        set => SetValue(ref field, value);
    }
}
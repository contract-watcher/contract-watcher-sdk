using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Data.Domain;

namespace ContractWatcher.SDK.Data.Mappings;

/// <summary>
/// Преобразует отчёты между транспортным контрактом Common и моделью хранения SDK.
/// </summary>
internal static class ReportMapping
{
    /// <summary>
    /// Создаёт ожидающий отправки отчёт с независимыми вложенными нарушениями.
    /// </summary>
    /// <param name="source">Результат проверки, сформированный SDK.</param>
    /// <param name="storedAt">Время помещения отчёта в локальное хранилище.</param>
    public static ReportEntity ToEntity(this ValidationReport source, DateTimeOffset storedAt)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var entity = new ReportEntity
        {
            ReportId = source.ReportId,
            Slug = source.Slug,
            ContractVersion = source.ContractVersion,
            Status = source.Status,
            Timestamp = source.Timestamp,
            StoredAt = storedAt
        };

        foreach (var violation in source.Violations)
            entity.Violations.Add(new ValidationViolationEntity
            {
                FieldName = violation.FieldName,
                Type = violation.Type,
                ExpectedType = violation.ExpectedType,
                ActualType = violation.ActualType
            });

        return entity;
    }

    /// <summary>
    /// Создаёт DTO для отправки, исключая локальные поля хранения и доставки.
    /// </summary>
    public static ValidationReport ToDto(this ReportEntity source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        return new ValidationReport
        {
            ReportId = source.ReportId,
            Slug = source.Slug,
            ContractVersion = source.ContractVersion,
            Status = source.Status,
            Timestamp = source.Timestamp,
            Violations = source.Violations
                .Select(violation => new ValidationViolation
                {
                    FieldName = violation.FieldName,
                    Type = violation.Type,
                    ExpectedType = violation.ExpectedType,
                    ActualType = violation.ActualType
                })
                .ToArray()
        };
    }
}
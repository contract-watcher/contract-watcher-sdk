using ContractWatcher.SDK.Data.Abstractions;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Представляет расписание и диагностику
/// повторной отправки отчёта.
/// </summary>
internal sealed class ReportRetryEntity : TrackedEntity
{
    /// <summary>
    /// Получает ключ записи и ссылку на ReportEntity.ReportId.
    /// </summary>
    public required Guid ReportId { get; init; }

    /// <summary>
    /// Получает или задаёт число неудач,
    /// включая первоначальную отправку.
    /// </summary>
    public int FailedAttempts
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт время последней неудачной отправки.
    /// </summary>
    public DateTimeOffset LastAttemptAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт время следующей попытки.
    /// Null означает прекращение автоматических попыток.
    /// </summary>
    public DateTimeOffset? NextAttemptAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт диагностическое описание
    /// последней ошибки отправки.
    /// </summary>
    public string? LastError
    {
        get;
        set => SetValue(ref field, value);
    }
}
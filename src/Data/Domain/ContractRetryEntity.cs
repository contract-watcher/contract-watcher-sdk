using ContractWatcher.SDK.Data.Abstractions;
using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Хранит этап доставки контракта и состояние повторных попыток.
/// </summary>
/// <remarks>
/// Содержимое контракта и назначенная версия находятся в ScannedContracts.
/// Revision связывает операцию с конкретной редакцией отсканированных правил.
/// </remarks>
internal sealed class ContractRetryEntity : TrackedEntity
{
    /// <summary>
    /// Slug связанного контракта.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Редакция отсканированных правил, для которой создана операция.
    /// </summary>
    public Guid Revision
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Этап, с которого необходимо продолжить доставку.
    /// </summary>
    public ScanningStage Stage
    {
        get;
        set => SetValue(ref field, value);
    } = ScanningStage.Synchronization;

    /// <summary>
    /// Количество неудачных попыток текущего этапа.
    /// </summary>
    public int FailedAttempts
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Время последней неудачной попытки.
    /// </summary>
    public DateTimeOffset? LastAttemptAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Время следующей попытки.
    /// </summary>
    /// <remarks>
    /// Null означает, что автоматические повторы остановлены:
    /// ошибка постоянная либо исчерпан лимит попыток.
    /// </remarks>
    public DateTimeOffset? NextAttemptAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Описание последней ошибки.
    /// </summary>
    public string? LastError
    {
        get;
        set => SetValue(ref field, value);
    }
}
using System.Text.Json.Serialization;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Data.Abstractions;
using ContractWatcher.SDK.Data.Enums;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Представляет сохранённый результат проверки
/// и состояние его доставки.
/// </summary>
internal sealed class ReportEntity : TrackedEntity
{
    /// <summary>
    /// Получает стабильный идентификатор отчёта,
    /// сохраняемый при повторных отправках.
    /// </summary>
    public required Guid ReportId { get; init; }

    /// <summary>
    /// Получает или задаёт имя проверенного контракта.
    /// </summary>
    public required string Slug
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт версию проверенного контракта.
    /// </summary>
    public required int ContractVersion
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт результат проверки контракта.
    /// </summary>
    public required ValidationStatus Status
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт исходное время формирования отчёта.
    /// </summary>
    public required DateTimeOffset Timestamp
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт время помещения отчёта в локальную базу.
    /// </summary>
    public required DateTimeOffset StoredAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт состояние доставки.
    /// По умолчанию отчёт ожидает отправки.
    /// </summary>
    public ReportDeliveryState DeliveryState
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт время подтверждения от Core.
    /// До подтверждения имеет значение null.
    /// </summary>
    public DateTimeOffset? DeliveredAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает нарушения, обнаруженные при проверке контракта.
    /// </summary>
    /// <remarks>
    /// Десериализация заполняет существующую коллекцию,
    /// сохраняя подключённые конструктором уведомления.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public TrackedCollection<ValidationViolationEntity> Violations
    {
        get;
    } = new();

    /// <summary>
    /// Создаёт сущность и подключает уведомления от нарушений.
    /// </summary>
    public ReportEntity() => Violations.Changed += OnCollectionChanged;

    /// <summary>
    /// Передаёт изменения нарушений трекеру отчёта.
    /// </summary>
    private void OnCollectionChanged(object? sender, EventArgs args) => OnChanged();
}
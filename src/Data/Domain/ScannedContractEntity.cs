using System.Text.Json.Serialization;
using ContractWatcher.SDK.Data.Abstractions;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Хранит желаемое содержимое обнаруженного контракта
/// и состояние его синхронизации.
/// </summary>
/// <remarks>
/// Не является опубликованным контрактом
/// и не используется валидатором.
/// </remarks>
internal sealed class ScannedContractEntity : TrackedEntity
{
    /// <summary>
    /// Стабильный ключ контракта текущего приложения.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Версия Core, относительно которой подготовлено изменение.
    /// Null — неизвестна; 0 — отсутствие контракта подтверждено.
    /// </summary>
    public int? ExpectedVersion
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Версия, назначенная SDK для публикации.
    /// Null — решение ещё не принято либо публикация не требуется.
    /// </summary>
    public int? TargetVersion
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Требуется сверка с Core и возможная публикация.
    /// True сохраняется до обращения к сети:
    /// ошибка или падение не теряют намерение.
    /// </summary>
    public bool Pending
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Время сохранения результата сканирования.
    /// </summary>
    public DateTimeOffset ScannedAt
    {
        get;
        set => SetValue(ref field, value);
    }
    
    /// <summary>
    /// Идентификатор редакции отсканированных правил.
    /// </summary>
    /// <remarks>
    /// Позволяет отличить актуальную операцию публикации
    /// от retry-записи предыдущей редакции.
    /// </remarks>
    public Guid Revision
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Желаемые правила.
    /// Вложенные изменения уведомляют MicroDb.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public TrackedCollection<ContractRuleEntity> Rules { get; } = new();

    /// <summary>
    /// Подключает события вложенной коллекции к трекеру сущности.
    /// </summary>
    public ScannedContractEntity() => Rules.Changed += (_, _) => OnChanged();
}
using System.Text.Json.Serialization;
using ContractWatcher.SDK.Data.Abstractions;

namespace ContractWatcher.SDK.Data.Domain;

/// <summary>
/// Представляет локальный контракт
/// с изменяемыми правилами проверки.
/// </summary>
internal sealed class ContractEntity : TrackedEntity
{
    /// <summary>
    /// Получает стабильный ключ контракта
    /// в рамках текущей интеграции.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Получает или задаёт опубликованную версию контракта.
    /// </summary>
    public required int ContractVersion
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает или задаёт время последнего обновления из Core.
    /// </summary>
    public required DateTimeOffset UpdatedAt
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Получает правила проверки контракта.
    /// Изменение любого правила уведомляет трекер контракта.
    /// </summary>
    /// <remarks>
    /// При чтении JSON заполняется существующая коллекция.
    /// Это сохраняет подписку владельца, созданную конструктором.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public TrackedCollection<ContractRuleEntity> Rules { get; } = new();

    /// <summary>
    /// Создаёт сущность и подключает уведомления от правил.
    /// </summary>
    public ContractEntity() => Rules.Changed += OnCollectionChanged;

    /// <summary>
    /// Передаёт изменения правил трекеру корневой сущности.
    /// </summary>
    private void OnCollectionChanged(object? sender, EventArgs args) => OnChanged();
}
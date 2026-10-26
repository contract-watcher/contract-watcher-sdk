using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Преобразует JSON Schema в транспортный контракт ContractWatcher.
/// </summary>
internal interface IContractMapper
{
    /// <summary>
    /// Формирует правила для свойств верхнего уровня.
    /// </summary>
    /// <param name="slug">Уникальное имя контракта.</param>
    /// <param name="version">Версия контракта.</param>
    /// <param name="schema">Схема входного JSON-объекта.</param>
    PublishedContract Map(string slug, int version, JsonNode schema);
}
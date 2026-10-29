using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Содержит результаты этапов одного запуска сканирования.
/// </summary>
/// <remarks>
/// Один экземпляр передаётся всем модулям
/// и сохраняется между попытками.
/// Null означает, что результат этапа ещё не сформирован.
/// </remarks>
public sealed class ScanningContext
{
    /// <summary>
    /// Обнаруженные endpoints.
    /// </summary>
    public IReadOnlyCollection<DiscoveredEndpoint>? Endpoints { get; set; }

    /// <summary>
    /// Снимки локальных контрактов до обращения к Core.
    /// Используются при ошибке запроса.
    /// </summary>
    public IReadOnlyDictionary<string, PublishedContract>? CachedContracts { get; set; }

    /// <summary>
    /// Результаты получения опубликованных контрактов из Core.
    /// </summary>
    public IReadOnlyCollection<ContractRefreshResult>? RefreshedContracts { get; set; }

    /// <summary>
    /// Приоритет источника, зафиксированный
    /// модулем синхронизации для текущего прохода.
    /// </summary>
    public ContractSourcePreference SourcePreference { get; set; }

    /// <summary>
    /// JSON Schema входных моделей, сгруппированные по CLR-типу.
    /// </summary>
    public IReadOnlyDictionary<Type, JsonNode>? Schemas { get; set; }

    /// <summary>
    /// Решения mapping для всех endpoints,
    /// включая контракты с пока неизвестной версией.
    /// </summary>
    public IReadOnlyCollection<ContractMappingResult>? MappingResults { get; set; }

    /// <summary>
    /// Выбранные контракты, включая кандидатов
    /// с назначенной SDK версией.
    /// </summary>
    /// <remarks>
    /// Наличие контракта здесь не означает успешную публикацию.
    /// Контракты без доступного содержимого и версии
    /// остаются только в MappingResults.
    /// </remarks>
    public IReadOnlyCollection<PublishedContract>? Contracts { get; set; }

    /// <summary>
    /// Имя текущего объекта для диагностики.
    /// Исполнитель сбрасывает значение перед повторной попыткой.
    /// </summary>
    public string? CurrentItem { get; set; }
}
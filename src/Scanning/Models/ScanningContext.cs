using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Содержит результаты этапов одного запуска сканирования.
/// </summary>
/// <remarks>
/// <para>
/// Один экземпляр передаётся всем модулям и сохраняется между попытками.
/// </para>
/// <para>
/// Null означает, что результат этапа ещё не сформирован.
/// Пустая коллекция означает успешное выполнение без результатов.
/// </para>
/// </remarks>
public sealed class ScanningContext
{
    /// <summary>
    /// Endpoints, для которых необходимо сформировать контракты.
    /// </summary>
    public IReadOnlyCollection<DiscoveredEndpoint>? Endpoints { get; set; }

    /// <summary>
    /// JSON Schema входных моделей, сгруппированные по CLR-типу.
    /// </summary>
    public IReadOnlyDictionary<Type, JsonNode>? Schemas { get; set; }

    /// <summary>
    /// Сформированные контракты для последующих этапов.
    /// </summary>
    public IReadOnlyCollection<PublishedContract>? Contracts { get; set; }

    /// <summary>
    /// Имя обрабатываемого объекта для диагностики:
    /// например, DTO или slug контракта.
    /// </summary>
    /// <remarks>
    /// Исполнитель сбрасывает значение перед каждой попыткой.
    /// </remarks>
    public string? CurrentItem { get; set; }
}
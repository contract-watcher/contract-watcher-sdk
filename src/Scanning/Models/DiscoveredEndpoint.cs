using System.Reflection;

namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Описывает MVC action, для которого необходимо сформировать контракт.
/// </summary>
internal sealed class DiscoveredEndpoint
{
    /// <summary>
    /// Уникальное имя контракта из ContractWatcherAttribute.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Метод контроллера, связанный с контрактом.
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// CLR-тип параметра, получаемого из тела запроса.
    /// </summary>
    public required Type RequestType { get; init; }
}
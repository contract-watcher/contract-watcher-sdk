using System.Reflection;

namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Описывает MVC action, для которого необходимо сформировать контракт.
/// </summary>
public sealed class DiscoveredEndpoint
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
    
    /// <summary>
    /// Маршруты action; несколько маршрутов используют один контракт.
    /// </summary>
    public IReadOnlyCollection<DiscoveredRoute> Routes { get; init; } = [];
}
namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Описывает один маршрут MVC action и допустимые HTTP-методы.
/// </summary>
public sealed class DiscoveredRoute
{
    /// <summary>
    /// Шаблон attribute route.
    /// Null означает conventional routing:
    /// конкретный URL нельзя восстановить только из ActionDescriptor.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// Допустимые HTTP-методы.
    /// Пустой список означает отсутствие ограничения.
    /// </summary>
    public IReadOnlyCollection<string> HttpMethods { get; init; } = [];
}
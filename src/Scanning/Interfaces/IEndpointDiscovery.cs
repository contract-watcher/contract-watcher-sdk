using ContractWatcher.SDK.Scanning.Models;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Обнаруживает endpoints, отмеченные для автоматического сбора контрактов.
/// </summary>
internal interface IEndpointDiscovery
{
    /// <summary>
    /// Находит endpoints и определяет типы их входных моделей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены поиска.</param>
    /// <returns>Обнаруженные endpoints без повторов.</returns>
    IReadOnlyCollection<DiscoveredEndpoint> Discover(CancellationToken cancellationToken = default);
}
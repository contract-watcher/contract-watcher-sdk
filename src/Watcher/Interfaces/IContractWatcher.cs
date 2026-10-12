using System.Text.Json;
using ContractWatcher.SDK.Watcher.Models;

namespace ContractWatcher.SDK.Watcher.Interfaces;

/// <summary>
/// Представляет основной сервис для явной проверки входящего JSON
/// </summary>
public interface IContractWatcher
{
    /// <summary>
    /// Выполняет проверку входящего JSON согласно контракту с указанным slug
    /// </summary>
    /// <param name="contractSlug">Slug контракта</param>
    /// <param name="payload">Входящий JSON</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Возвращает локальный результат проверки</returns>
    Task<ContractWatchResult> ValidateAsync(string contractSlug, JsonElement payload, CancellationToken cancellationToken = default);
}
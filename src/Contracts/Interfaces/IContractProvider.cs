using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Interfaces;

/// <summary>
/// Предоставляет опубликованные контракты, используемые SDK для локальной проверки входящих данных
/// </summary>
public interface IContractProvider
{
    /// <summary>
    /// Получает опубликованный контракт по его slug
    /// </summary>
    /// <param name="slug">Уникальное имя контракта в рамках текущей интеграции</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns> Возвращает опубликованный контракт либо null, если контракт получить не удалось</returns>
    Task<PublishedContract?> GetAsync(string slug, CancellationToken cancellationToken = default);
}
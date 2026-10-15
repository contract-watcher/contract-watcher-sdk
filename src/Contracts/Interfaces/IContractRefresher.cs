using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Interfaces;

/// <summary>
/// Обновляет опубликованные контракты из внешнего источника
/// </summary>
public interface IContractRefresher
{
    /// <summary>
    /// Загружает актуальную версию контракта из внешнего источника и обновляет его локальное представление
    /// </summary>
    /// <param name="slug">Уникальное имя контракта в рамках текущей интеграции</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>
    /// Возвращает актуальный опубликованный контракт либо null, если контракт с указанным slug отсутствует
    /// </returns>
    Task<PublishedContract?> RefreshAsync(string slug, CancellationToken cancellationToken = default);
}
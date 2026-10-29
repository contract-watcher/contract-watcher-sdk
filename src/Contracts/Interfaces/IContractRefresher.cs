using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Models;

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

    /// <summary>
    /// Обновляет набор контрактов и возвращает результат для каждого slug.
    /// </summary>
    /// <remarks>
    /// Стандартный refresher сохраняет успешные ответы пакетом.
    /// Совместимая реализация вызывает пользовательский RefreshAsync
    /// последовательно; политика ошибок остаётся за пользовательским refresher.
    /// </remarks>
    Task<IReadOnlyCollection<ContractRefreshResult>> RefreshManyAsync(
        IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken = default);
}
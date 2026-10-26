using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Interfaces;

/// <summary>
/// Представляет локальное хранилище опубликованных контрактов
/// </summary>
/// <remarks>Контракты идентифицируются по уникальному slug в рамках текущей интеграции</remarks>
public interface IContractStore
{
    /// <summary>
    /// Получает slug всех контрактов, сохранённых в локальном хранилище
    /// </summary>
    /// <returns>Коллекция slug сохранённых контрактов</returns>
    IReadOnlyCollection<string> GetSlugs();
    
    /// <summary>
    /// Получает локально сохранённый контракт по его slug
    /// </summary>
    /// <param name="slug">Уникальное имя контракта в рамках интеграции</param>
    /// <returns>Возвращает сохранённый контракт либо null, если контракт с указанным slug отсутствует</returns>
    PublishedContract? Get(string slug);

    /// <summary>
    /// Сохраняет новый контракт или обновляет существующий.
    /// </summary>
    /// <param name="contract">Контракт для сохранения.</param>
    /// <param name="cancellationToken">Токен отмены записи.</param>
    Task SetAsync(PublishedContract contract, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет контракт и сохраняет изменение.
    /// </summary>
    /// <param name="slug">Уникальное имя контракта.</param>
    /// <param name="cancellationToken">Токен отмены записи.</param>
    /// <returns>True, если контракт существовал и был удалён.</returns>
    Task<bool> RemoveAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет все контракты и сохраняет изменение.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены записи.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
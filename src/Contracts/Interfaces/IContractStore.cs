using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Interfaces;

/// <summary>
/// Представляет локальное хранилище опубликованных контрактов
/// </summary>
/// <remarks>
/// Контракты идентифицируются по уникальному slug в рамках текущей интеграции
/// </remarks>
public interface IContractStore
{
    /// <summary>
    /// Получает локально сохранённый контракт по его slug
    /// </summary>
    /// <param name="slug">Уникальное имя контракта в рамках интеграции</param>
    /// <returns>
    /// Сохранённый контракт либо null, если контракт с указанным slug отсутствует
    /// </returns>
    PublishedContract? Get(string slug);

    /// <summary>
    /// Сохраняет контракт в локальном хранилище
    /// </summary>
    /// <remarks>
    /// Если контракт с таким же slug уже существует, он полностью заменяется новым значением
    /// Сравнение версий не является ответственностью хранилища
    /// </remarks>
    /// <param name="contract">Контракт для сохранения</param>
    void Set(PublishedContract contract);

    /// <summary>
    /// Удаляет контракт из локального хранилища
    /// </summary>
    /// <param name="slug">Уникальное имя удаляемого контракта</param>
    /// <returns>
    /// Возвращает true, если контракт существовал и был удалён; false
    /// </returns>
    bool Remove(string slug);

    /// <summary>
    /// Удаляет все контракты из локального хранилища
    /// </summary>
    void Clear();
}
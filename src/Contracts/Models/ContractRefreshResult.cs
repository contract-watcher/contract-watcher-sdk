using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Models;

/// <summary>
/// Результат обновления одного контракта из Core.
/// </summary>
public sealed class ContractRefreshResult
{
    /// <summary>
    /// Slug запрошенного контракта.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Полученный контракт.
    /// Null при отсутствии контракта либо ошибке запроса.
    /// </summary>
    public PublishedContract? Contract { get; init; }

    /// <summary>
    /// Ошибка запроса.
    /// Null означает, что запрос завершился успешно.
    /// </summary>
    public Exception? Error { get; init; }
}
using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Core.Interfaces;

/// <summary>
/// Представляет HTTP-клиент для взаимодействия ContractWatcher SDK с Core
/// </summary>
public interface IContractWatcherCoreClient
{
    /// <summary>
    /// Получает опубликованный контракт по его slug
    /// </summary>
    Task<PublishedContract?> GetContractAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет отчёт о результате проверки в Core
    /// </summary>
    Task PublishReportAsync(ValidationReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Публикует правила и возвращает контракт с версией, назначенной Core.
    /// </summary>
    Task<PublishedContract> PublishContractAsync(
        PublishedContract contract, CancellationToken cancellationToken = default);
}
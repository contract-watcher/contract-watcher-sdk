using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Reporting.Interfaces;

/// <summary>
/// Публикует отчёты проверки контрактов
/// </summary>
public interface IReportPublisher
{
    /// <summary>
    /// Публикует отчёт проверки контракта
    /// </summary>
    /// <param name="report">Отчёт проверки контракта</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task PublishAsync(ValidationReport report, CancellationToken cancellationToken = default);
}
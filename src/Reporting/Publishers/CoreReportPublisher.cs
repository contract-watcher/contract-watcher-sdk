using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Services;

namespace ContractWatcher.SDK.Reporting.Publishers;

/// <summary>
/// Сохраняет отчёт в MicroDb и инициирует отправку с последующим фоновым повтором.
/// </summary>
/// <remarks>
/// Успех при недоступном Core означает, что отчёт сохранён и ожидает повторной отправки.
/// </remarks>
internal sealed class CoreReportPublisher(ReportDeliveryService delivery) : IReportPublisher
{
    public Task PublishAsync(ValidationReport report, CancellationToken cancellationToken = default) =>
        delivery.PublishAsync(report, cancellationToken);
}
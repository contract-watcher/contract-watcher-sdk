using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Reporting.Interfaces;

namespace ContractWatcher.SDK.Reporting.Publishers;

/// <summary>
/// Публикует отчёты проверки в ContractWatcher Core
/// </summary>
public sealed class CoreReportPublisher(IContractWatcherCoreClient coreClient) : IReportPublisher
{
    public Task PublishAsync(ValidationReport report, CancellationToken cancellationToken = default)
    {
        if (report is null)
            throw new ArgumentNullException(nameof(report));

        return coreClient.PublishReportAsync(report, cancellationToken);
    }
}
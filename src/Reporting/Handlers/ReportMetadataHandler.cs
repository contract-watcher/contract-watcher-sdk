using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Models;

namespace ContractWatcher.SDK.Reporting.Handlers;

/// <summary>
/// Добавляет общие метаданные отчета проверки
/// </summary>
internal sealed class ReportMetadataHandler : IReportBuildHandler
{
    private readonly TimeProvider _timeProvider;

    public ReportMetadataHandler(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public void Handle(ReportBuildContext context, ReportBuildDelegate next)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));

        context.ReportId = Guid.NewGuid();
        context.Timestamp = _timeProvider.GetUtcNow();

        next(context);
    }
}
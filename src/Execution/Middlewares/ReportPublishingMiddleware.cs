using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Reporting.Interfaces;

namespace ContractWatcher.SDK.Execution.Middlewares;

/// <summary>
/// Публикует сформированный отчёт проверки контракта
/// </summary>
internal sealed class ReportPublishingMiddleware(IReportPublisher reportPublisher) : IWatcherMiddleware
{
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));

        var report = context.Report ?? throw new InvalidOperationException("Report must be built before publishing.");

        await reportPublisher.PublishAsync(report, cancellationToken);

        await next(context, cancellationToken);
    }
}
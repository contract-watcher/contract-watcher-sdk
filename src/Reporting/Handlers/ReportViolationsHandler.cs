using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Models;

namespace ContractWatcher.SDK.Reporting.Handlers;

/// <summary>
/// Добавляет нарушения контракта в неуспешный отчет проверки
/// </summary>
public sealed class ReportViolationsHandler : IReportBuildHandler
{
    public void Handle(ReportBuildContext context, ReportBuildDelegate next)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));

        context.Violations = context.ValidationResult.Violations;

        next(context);
    }
}
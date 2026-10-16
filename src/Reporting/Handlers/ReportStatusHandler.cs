using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Models;

namespace ContractWatcher.SDK.Reporting.Handlers;

/// <summary>
/// Определяет статус отчета на основании результата проверки
/// </summary>
internal sealed class ReportStatusHandler : IReportBuildHandler
{
    public void Handle(ReportBuildContext context, ReportBuildDelegate next)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));
        
        // Если нарушений не было, то отчет сформирован и готов к отправке
        if (context.ValidationResult.IsValid)
        {
            context.Status = ValidationStatus.Passed;
            return;
        }

        context.Status = ValidationStatus.Failed;

        next(context);
    }
}
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Models;

namespace ContractWatcher.SDK.Reporting.Interfaces;

/// <summary>
/// Представляет этап цепочки формирования отчета проверки
/// </summary>
public interface IReportBuildHandler
{
    /// <summary>
    /// Обрабатывает контекст формирования отчета и при необходимости передает его следующему обработчику
    /// </summary>
    void Handle(ReportBuildContext context, ReportBuildDelegate next);
}
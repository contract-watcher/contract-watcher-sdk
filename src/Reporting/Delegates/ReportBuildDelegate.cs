using ContractWatcher.SDK.Reporting.Models;

namespace ContractWatcher.SDK.Reporting.Delegates;

/// <summary>
/// Представляет следующий обработчик цепочки формирования отчета
/// </summary>
/// <param name="context">Контекст формирования отчета</param>
public delegate void ReportBuildDelegate(ReportBuildContext context);
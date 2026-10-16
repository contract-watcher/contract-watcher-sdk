using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Models;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Reporting.Builders;

/// <summary>
/// Формирует отчет проверки, последовательно выполняя зарегистрированные обработчики
/// </summary>
internal sealed class ValidationReportBuilder : IValidationReportBuilder
{
    /// <summary>
    /// Цепочка формирования отчета
    /// </summary>
    private readonly ReportBuildDelegate _chain;

    public ValidationReportBuilder(IEnumerable<IReportBuildHandler> handlers)
    {
        if (handlers is null)
            throw new ArgumentNullException(nameof(handlers));

        _chain = BuildChain(handlers.ToArray());
    }

    public ValidationReport Build(PublishedContract contract, ContractValidationResult validationResult)
    {
        if (contract is null)
            throw new ArgumentNullException(nameof(contract));
        
        if (validationResult is null)
            throw new ArgumentNullException(nameof(validationResult));

        var context = new ReportBuildContext
        {
            Contract = contract,
            ValidationResult = validationResult
        };

        _chain(context);

        if (context.Status is null)
            throw new InvalidOperationException("Report status was not assigned during report building.");

        return new ValidationReport
        {
            Slug = contract.Slug,
            ReportId = context.ReportId,
            ContractVersion = contract.ContractVersion,
            Status = context.Status.Value,
            // TODO: раскомментировать после обновления ContractWatcher.Common
            // Timestamp = context.Timestamp, 
            Violations = context.Violations
        };
    }

    /// <summary>
    /// Сборка цепочки формирования отчета
    /// </summary>
    /// <param name="handlers">Обработчики для формирования отчета</param>
    /// <returns>Возвращает цепочку обработки отчета</returns>
    private static ReportBuildDelegate BuildChain(IReadOnlyList<IReportBuildHandler> handlers)
    {
        ReportBuildDelegate chain = static _ => { };

        for (var index = handlers.Count - 1; index >= 0; index--)
        {
            var handler = handlers[index];
            var next = chain;

            chain = context => handler.Handle(context, next);
        }

        return chain;
    }
}
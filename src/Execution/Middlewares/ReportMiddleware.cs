using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Reporting.Interfaces;

namespace ContractWatcher.SDK.Execution.Middlewares;

/// <summary>
/// Формирует отчет на основе результата проверки контракта
/// </summary>
public sealed class ReportMiddleware : IWatcherMiddleware
{
    private readonly IValidationReportBuilder _reportBuilder;

    /// <summary>
    /// Инициализирует новый экземпляр middleware формирования отчета
    /// </summary>
    /// <param name="reportBuilder">Builder, необходимый для формирования отчета проверки</param>
    public ReportMiddleware(IValidationReportBuilder reportBuilder) =>
        _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));

    /// <inheritdoc />
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));

        var contract = context.Contract ?? throw new InvalidOperationException(
            "Контакт должен быть получен перед формированием отчета.");

        var validationResult = context.ValidationResult ?? throw new InvalidOperationException(
            "Результат валидации должен быть получен перед формированием отчета.");

        context.Report = _reportBuilder.Build(contract, validationResult);

        await next(context, cancellationToken);
    }
}
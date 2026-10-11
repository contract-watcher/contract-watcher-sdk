using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Validation.Interfaces;

namespace ContractWatcher.SDK.Execution.Middlewares;

/// <summary>
/// Выполняет локальную проверку JSON по разрешённому контракту
/// </summary>
public sealed class ValidationMiddleware : IWatcherMiddleware
{
    private readonly IContractValidator _validator;

    /// <summary>
    /// Инициализирует новый экземпляр middleware локальной проверки
    /// </summary>
    /// <param name="validator">Валидатор входящего JSON</param>
    public ValidationMiddleware(IContractValidator validator) =>
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is  null)
            throw new ArgumentNullException(nameof(next));

        var contract = 
            context.Contract ?? throw new InvalidOperationException("Contract must be resolved before validation.");

        var result = _validator.Validate(context.Payload, contract);

        context.ValidationResult = result;
        context.Status = result.IsValid ? WatcherStatus.Passed : WatcherStatus.Failed;

        await next(context, cancellationToken);
    }
}
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Execution.Middlewares;

/// <summary>
/// Разрешает опубликованный контракт перед выполнением проверки
/// </summary>
public sealed class ContractResolutionMiddleware : IWatcherMiddleware
{
    private readonly IContractProvider _contractProvider;

    /// <summary>
    /// Инициализирует новый экземпляр middleware разрешения контракта
    /// </summary>
    /// <param name="contractProvider">Провайдер опубликованных контрактов</param>
    public ContractResolutionMiddleware(IContractProvider contractProvider) =>
        _contractProvider = contractProvider ?? throw new ArgumentNullException(nameof(contractProvider));
    
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is  null)
            throw new ArgumentNullException(nameof(next));

        var contract = await _contractProvider.GetAsync(context.ContractSlug, cancellationToken);

        if (contract is null)
        {
            context.Status = WatcherStatus.ContractUnavailable;

            return;
        }

        context.Contract = contract;
        
        await next(context, cancellationToken);
    }
}
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using Microsoft.Extensions.Logging;

namespace ContractWatcher.SDK.Execution.Middlewares;

/// <summary>
/// Разрешает опубликованный контракт перед выполнением проверки
/// </summary>
public sealed class ContractResolutionMiddleware : IWatcherMiddleware
{
    /// <summary>
    /// Провадер контрактов
    /// </summary>
    private readonly IContractProvider _contractProvider;
    
    /// <summary>
    /// Логгер для ошибок
    /// </summary>
    private readonly ILogger<ContractResolutionMiddleware> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр middleware разрешения контракта
    /// </summary>
    /// <param name="contractProvider">Провайдер опубликованных контрактов</param>
    /// <param name="logger">Логгер для ошибок</param>
    public ContractResolutionMiddleware(IContractProvider contractProvider, ILogger<ContractResolutionMiddleware> logger)
    {
        _contractProvider = contractProvider ?? throw new ArgumentNullException(nameof(contractProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is  null)
            throw new ArgumentNullException(nameof(next));

        try
        {
            var contract = await _contractProvider.GetAsync(context.ContractSlug, cancellationToken);

            if (contract is null)
            {
                context.Status = WatcherStatus.ContractUnavailable;
                return;
            }
            
            context.Contract = contract;
        }
        catch (CoreRequestException exception)
        {
            _logger.LogWarning(exception, "Failed to resolve contract '{ContractSlug}' from Core", context.ContractSlug);
            context.Status = WatcherStatus.ContractUnavailable;

            return;
        }
        
        await next(context, cancellationToken);
    }
}
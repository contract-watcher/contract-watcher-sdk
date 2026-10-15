using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using Microsoft.Extensions.Logging;

namespace ContractWatcher.SDK.Execution.Middlewares;

public sealed class TechnicalErrorMiddleware(ILogger<TechnicalErrorMiddleware> logger) : IWatcherMiddleware
{
    public async Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is  null)
            throw new ArgumentNullException(nameof(next));

        try
        {
            await next(context, cancellationToken);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected technical error occurred while processing contract '{ContractSlug}'.", context.ContractSlug);
            context.Status = WatcherStatus.TechnicalError;
        }
    }
}
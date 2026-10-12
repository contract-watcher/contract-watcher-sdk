using System.Text.Json;
using ContractWatcher.SDK.AspNetCore.Attributes;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace ContractWatcher.SDK.AspNetCore.Filters;

/// <summary>
/// Выполняет автоматическую проверку JSON для endpoint, помеченных атрибутом <see cref="ContractWatcherAttribute"/>
/// </summary>
public sealed class ContractWatcherResourceFilter : IAsyncResourceFilter
{
    private readonly IContractWatcher _contractWatcher;
    private readonly ILogger<ContractWatcherResourceFilter> _logger;
    
    public ContractWatcherResourceFilter(IContractWatcher contractWatcher, ILogger<ContractWatcherResourceFilter> logger)
    {
        _contractWatcher = contractWatcher ?? throw new ArgumentNullException(nameof(contractWatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (next is null)
            throw new ArgumentNullException(nameof(next));

        var attribute = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ContractWatcherAttribute>();

        if (attribute is null)
        {
            await next();
            return;
        }

        await ValidateAsync(context, attribute.ContractSlug);

        await next();
    }

    private async Task ValidateAsync(ResourceExecutingContext context, string contractSlug)
    {
        var request = context.HttpContext.Request;
        var cancellationToken = context.HttpContext.RequestAborted;

        // Разрешение на чтение запроса несколько раз (приложение + обработчик)
        request.EnableBuffering();

        try
        {
            request.Body.Position = 0;

            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            var payload = document.RootElement.Clone();

            await _contractWatcher.ValidateAsync(contractSlug, payload, cancellationToken);
        }
        catch (JsonException exception)
        {
            // Перехват исключений, чтобы не ломать пользовательское приложение
            _logger.LogWarning(exception, "ContractWatcher could not parse the request body as JSON.");
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
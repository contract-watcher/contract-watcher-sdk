using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using Microsoft.Extensions.Options;

using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Формирует схемы входных моделей с настройками JSON пользовательского MVC.
/// </summary>
public sealed class SchemaGenerationModule(IOptions<MvcJsonOptions> options) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Schema;
    
    public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var endpoints = context.Endpoints ?? 
            throw new InvalidOperationException("Endpoint discovery must complete before schema generation.");

        var serializerOptions = new JsonSerializerOptions(options.Value.JsonSerializerOptions);

        var schemas = new Dictionary<Type, JsonNode>();

        foreach (var requestType in endpoints.Select(endpoint => endpoint.RequestType).Distinct())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            context.CurrentItem = requestType.FullName;

            schemas.Add(requestType, serializerOptions.GetJsonSchemaAsNode(requestType));
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        // При ошибке локальный словарь отбрасывается.
        // В контекст попадает только полный результат этапа.
        context.Schemas = schemas;

        return Task.CompletedTask;
    }
}
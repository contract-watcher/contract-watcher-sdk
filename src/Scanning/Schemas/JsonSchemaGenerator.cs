using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using ContractWatcher.SDK.Scanning.Interfaces;
using Microsoft.Extensions.Options;

using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace ContractWatcher.SDK.Scanning.Schemas;

/// <summary>
/// Генерирует JSON Schema с помощью System.Text.Json.
/// </summary>
internal sealed class JsonSchemaGenerator : IJsonSchemaGenerator
{
    /// <summary>
    /// Копия настроек JSON пользовательского MVC-приложения.
    /// </summary>
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Создаёт генератор с настройками сериализации MVC.
    /// </summary>
    public JsonSchemaGenerator(IOptions<MvcJsonOptions> options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _options = new JsonSerializerOptions(options.Value.JsonSerializerOptions);
    }
    
    public JsonNode Generate(Type requestType)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        return _options.GetJsonSchemaAsNode(requestType);
    }
}
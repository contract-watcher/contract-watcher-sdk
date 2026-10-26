using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Schemas;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Преобразует схемы в контракты с правилами верхнего уровня.
/// </summary>
/// <remarks>
/// Вложенные объекты и массивы описываются только типом,
/// обязательностью и допустимостью null.
/// </remarks>
public sealed class ContractMappingModule : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Mapping;
    
    public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var endpoints = context.Endpoints ??
            throw new InvalidOperationException("Endpoint discovery must complete before mapping.");

        var schemas = context.Schemas ??
            throw new InvalidOperationException("Schema generation must complete before mapping.");

        var contracts = new List<PublishedContract>(endpoints.Count);

        foreach (var endpoint in endpoints)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            context.CurrentItem = endpoint.Slug;

            if (!schemas.TryGetValue(endpoint.RequestType, out var schema))
                throw new InvalidOperationException($"Schema for '{endpoint.RequestType}' was not generated.");

            // TODO: согласовать назначение версии с Core
            // Повтор этапа не должен сам увеличивать версию.
            contracts.Add(Map(endpoint.Slug, version: 1, schema: schema));
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        context.Contracts = contracts.ToArray();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Формирует контракт из свойств корневой схемы.
    /// </summary>
    private static PublishedContract Map(string slug, int version, JsonNode schema)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Contract slug is required.", nameof(slug));

        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        if (schema is null)
            throw new ArgumentNullException(nameof(schema));

        var root = JsonSchemaReferenceResolver.Resolve(schema, schema);

        if (GetFieldType(root, slug) != ContractFieldType.Object)
            throw new NotSupportedException($"Contract '{slug}' must describe a JSON object.");

        var required = ReadRequiredProperties(root);
        var rules = new List<ContractRule>();

        if (root["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
            {
                if (property.Value is null)
                    throw new NotSupportedException($"Property '{slug}.{property.Key}' has no schema.");

                var propertySchema = 
                    JsonSchemaReferenceResolver.Resolve(property.Value, schema);

                var fieldType = GetFieldType(propertySchema, $"{slug}.{property.Key}");

                rules.Add(new ContractRule
                {
                    FieldName = property.Key,
                    Type = fieldType,
                    Required = required.Contains(property.Key),
                    Nullable = ReadTypes(propertySchema).Contains("null")
                });
            }
        }

        return new PublishedContract
        {
            Slug = slug,
            ContractVersion = version,
            Rules = rules.ToArray()
        };
    }

    /// <summary>
    /// Читает имена обязательных свойств корневого объекта.
    /// </summary>
    private static HashSet<string> ReadRequiredProperties(JsonNode schema)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);

        if (schema["required"] is not JsonArray properties)
            return required;

        foreach (var property in properties)
            required.Add(property!.GetValue<string>());

        return required;
    }

    /// <summary>
    /// Определяет единственный представимый тип поля.
    /// </summary>
    /// <remarks>
    /// Null обрабатывается отдельно.
    /// Integer и number объединяются в ContractFieldType.Number.
    /// </remarks>
    private static ContractFieldType GetFieldType(JsonNode schema, string fieldName)
    {
        if (schema is not JsonObject schemaObject ||
            schemaObject.ContainsKey("anyOf") ||
            schemaObject.ContainsKey("oneOf") ||
            schemaObject.ContainsKey("allOf") ||
            schemaObject.ContainsKey("not"))
            throw new NotSupportedException($"Schema for '{fieldName}' is not supported.");

        var types = ReadTypes(schema)
            .Where(type => type != "null")
            .Select(type => MapType(type, fieldName))
            .Distinct()
            .ToArray();

        if (types.Length != 1)
            throw new NotSupportedException(
                $"Schema for '{fieldName}' must have one supported non-null type.");

        return types[0];
    }

    /// <summary>
    /// Преобразует имя типа JSON Schema в тип поля контракта.
    /// </summary>
    private static ContractFieldType MapType(string type, string fieldName) =>
        type switch
        {
            "string" => ContractFieldType.String,
            "integer" or "number" => ContractFieldType.Number,
            "boolean" => ContractFieldType.Boolean,
            "object" => ContractFieldType.Object,
            "array" => ContractFieldType.Array,

            _ => throw new NotSupportedException(
                $"JSON type '{type}' for '{fieldName}' is not supported.")
        };

    /// <summary>
    /// Читает type, представленный строкой или массивом строк.
    /// </summary>
    private static string[] ReadTypes(JsonNode schema) =>
        schema["type"] switch
        {
            JsonValue value => [value.GetValue<string>()],

            JsonArray values => values
                .Select(value => value!.GetValue<string>())
                .ToArray(),

            _ => []
        };
}
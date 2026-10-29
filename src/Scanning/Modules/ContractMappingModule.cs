using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Contracts.Comparers;
using ContractWatcher.SDK.Contracts.Models;
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

        var endpoints = context.Endpoints 
            ?? throw new InvalidOperationException("Endpoint discovery must complete before mapping.");

        var schemas = context.Schemas
            ?? throw new InvalidOperationException("Schema generation must complete before mapping.");

        var refreshed = (context.RefreshedContracts
            ?? throw new InvalidOperationException("Contract synchronization must complete before mapping."))
            .ToDictionary(result => result.Slug, StringComparer.Ordinal);

        var results = new List<ContractMappingResult>(endpoints.Count);

        foreach (var endpoint in endpoints)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            context.CurrentItem = endpoint.Slug;

            if (!schemas.TryGetValue(endpoint.RequestType, out var schema))
                throw new InvalidOperationException($"Schema for '{endpoint.RequestType}' was not generated.");

            var rules = MapRules(endpoint.Slug, schema);

            if (!refreshed.TryGetValue(endpoint.Slug, out var refresh))
                throw new InvalidOperationException($"Refresh result for '{endpoint.Slug}' is missing.");

            results.Add(Resolve(context, endpoint.Slug, rules, refresh));
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        context.MappingResults = results.ToArray();

        context.Contracts = results
            .Where(result => result.Contract is not null)
            .Select(result => result.Contract!)
            .ToArray();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Формирует контракт из свойств корневой схемы.
    /// </summary>
    /// <summary>
    /// Формирует правила из свойств корневой схемы без назначения версии.
    /// </summary>
    private static IReadOnlyCollection<ContractRule> MapRules(string slug, JsonNode schema)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Contract slug is required.", nameof(slug));

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

                var propertySchema = JsonSchemaReferenceResolver.Resolve(property.Value, schema);
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

        return rules.ToArray();
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
    
    /// <summary>
    /// Выбирает содержимое и назначает версию
    /// относительно полученного снимка Core.
    /// </summary>
    /// <remarks>
    /// Повтор mapping использует тот же снимок
    /// и не увеличивает версию повторно.
    /// При ошибке Core новая версия не назначается.
    /// </remarks>
    private static ContractMappingResult Resolve(
        ScanningContext context,
        string slug,
        IReadOnlyCollection<ContractRule> rules,
        ContractRefreshResult refresh)
    {
        var application = context.SourcePreference == ContractSourcePreference.Application;

        var published = refresh.Contract;

        if (refresh.Error is not null)
        {
            return new ContractMappingResult
            {
                Slug = slug,
                Rules = rules,
                Contract = context.CachedContracts?.GetValueOrDefault(slug),
                Pending = application
            };
        }

        if (published is not null &&
            (!StringComparer.Ordinal.Equals(slug, published.Slug) || published.ContractVersion <= 0))
            throw new InvalidOperationException("Refresher returned an incompatible contract.");

        var expectedVersion = published?.ContractVersion ?? 0;

        var pending = application && 
            (published is null || !ContractContentComparer.AreEqual(slug, rules, published));

        int? targetVersion = pending ? checked(expectedVersion + 1) : null;

        return new ContractMappingResult
        {
            Slug = slug,
            Rules = rules,
            ExpectedVersion = expectedVersion,
            TargetVersion = targetVersion,
            Pending = pending,
            Contract = targetVersion.HasValue ? new PublishedContract
                {
                    Slug = slug,
                    Rules = rules,
                    ContractVersion = targetVersion.Value
                } : published
        };
    }
}
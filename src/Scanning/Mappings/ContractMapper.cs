using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Schemas;

namespace ContractWatcher.SDK.Scanning.Mappings;

/// <summary>
/// Преобразует свойства корневой JSON Schema в правила ContractWatcher.
/// </summary>
/// <remarks>
/// Объекты и массивы описываются только типом, required и nullable.
/// Ограничения enum, format, длины и диапазоны в ContractRule не переносятся.
/// </remarks>
internal sealed class ContractMapper : IContractMapper
{
    public PublishedContract Map(string slug, int version, JsonNode schema)
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
    /// Неоднозначные и неограниченные схемы явно отклоняются.
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
            throw new NotSupportedException($"Schema for '{fieldName}' must have one supported non-null type.");

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
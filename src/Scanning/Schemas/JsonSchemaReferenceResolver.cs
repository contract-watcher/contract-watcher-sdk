using System.Globalization;
using System.Text.Json.Nodes;

namespace ContractWatcher.SDK.Scanning.Schemas;

/// <summary>
/// Разрешает локальные JSON Pointer ссылки внутри JSON Schema.
/// </summary>
internal static class JsonSchemaReferenceResolver
{
    /// <summary>
    /// Возвращает схему, на которую указывает цепочка локальных ссылок.
    /// </summary>
    /// <param name="schema">Исходный узел схемы.</param>
    /// <param name="root">Корень документа, относительно которого заданы ссылки.</param>
    /// <remarks>
    /// Не загружает внешние документы и не обходит дочерние свойства.
    /// Циклические цепочки ссылок и ссылки с дополнительными
    /// ограничениями явно отклоняются.
    /// </remarks>
    public static JsonNode Resolve(JsonNode schema, JsonNode root)
    {
        if (schema is null)
            throw new ArgumentNullException(nameof(schema));
        
        if (root is null)
            throw new ArgumentNullException(nameof(root));

        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (schema is JsonObject schemaObject && schemaObject["$ref"] is JsonValue referenceValue)
        {
            if (schemaObject.Count != 1)
                throw new NotSupportedException("Schemas combining $ref with other keywords are not supported.");

            var reference = referenceValue.GetValue<string>();

            if (!visited.Add(reference))
                throw new NotSupportedException($"Circular schema reference: '{reference}'.");

            schema = ResolvePointer(reference, root);
        }

        return schema;
    }

    /// <summary>
    /// Находит узел по локальному JSON Pointer.
    /// </summary>
    private static JsonNode ResolvePointer(string reference, JsonNode root)
    {
        if (reference == "#")
            return root;

        if (!reference.StartsWith("#/", StringComparison.Ordinal))
            throw new NotSupportedException($"Only local schema references are supported: '{reference}'.");

        var pointer = Uri.UnescapeDataString(reference[2..]);
        JsonNode? current = root;

        foreach (var segment in pointer.Split('/'))
        {
            var key = segment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            current = current switch
            {
                JsonObject obj => obj[key],

                JsonArray array when
                    int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                    index < array.Count => array[index],

                _ => null
            };

            if (current is null)
                throw new InvalidOperationException($"Schema reference '{reference}' cannot be resolved.");
        }

        return current!;
    }
}
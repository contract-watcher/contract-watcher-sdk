using System.Text.Json;
using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Contracts.Comparers;

/// <summary>
/// Сравнивает содержимое контрактов
/// независимо от версии и порядка правил.
/// </summary>
internal static class ContractContentComparer
{
    /// <summary>
    /// Сравнивает содержимое двух контрактов.
    /// </summary>
    internal static bool AreEqual(PublishedContract left, PublishedContract right) => AreEqual(left.Slug, left.Rules, right);

    /// <summary>
    /// Сравнивает отсканированные правила
    /// с опубликованным контрактом.
    /// </summary>
    internal static bool AreEqual(string slug, IReadOnlyCollection<ContractRule> rules, PublishedContract published) =>
        JsonNode.DeepEquals(Normalize(slug, rules), Normalize(published.Slug, published.Rules));

    /// <summary>
    /// Исключает версию и стабилизирует порядок правил.
    /// Сортировка сохраняет повторяющиеся элементы.
    /// </summary>
    private static JsonNode? Normalize(string slug, IReadOnlyCollection<ContractRule> rules) =>
        JsonSerializer.SerializeToNode(new
        {
            Slug = slug,
            Rules = rules
                .OrderBy(rule => rule.FieldName, StringComparer.Ordinal)
                .ThenBy(rule => rule.Type)
                .ThenBy(rule => rule.Required)
                .ThenBy(rule => rule.Nullable)
                .Select(rule => new
                {
                    rule.FieldName,
                    rule.Type,
                    rule.Required,
                    rule.Nullable
                })
                .ToArray()
        });
}
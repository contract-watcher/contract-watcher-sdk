using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Data.Domain;

namespace ContractWatcher.SDK.Data.Mappings;

/// <summary>
/// Преобразует опубликованные контракты Core в модель хранения SDK и обратно.
/// </summary>
internal static class ContractMapping
{
    /// <summary>
    /// Создаёт новую сущность и независимые вложенные правила из DTO.
    /// </summary>
    /// <param name="source">Контракт, полученный от Core.</param>
    /// <param name="updatedAt">Время получения контракта.</param>
    public static ContractEntity ToEntity(this PublishedContract source, DateTimeOffset updatedAt)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var entity = new ContractEntity
        {
            Slug = source.Slug,
            ContractVersion = source.ContractVersion,
            UpdatedAt = updatedAt
        };

        entity.Apply(source, updatedAt);

        return entity;
    }

    /// <summary>
    /// Обновляет найденную tracked-сущность данными Core,
    /// сохраняя её ключ и экземпляр.
    /// </summary>
    /// <param name="target">Существующая сущность из DbSet.</param>
    /// <param name="source">Опубликованная версия контракта.</param>
    /// <param name="updatedAt">Время получения версии.</param>
    public static void Apply(this ContractEntity target, PublishedContract source, DateTimeOffset updatedAt)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        if (!StringComparer.Ordinal.Equals(target.Slug, source.Slug))
            throw new ArgumentException("Cannot change the key of a tracked contract.", nameof(source));

        // Создаём правила до изменения существующей сущности.
        var rules = source.Rules
            .Select(rule => new ContractRuleEntity
            {
                FieldName = rule.FieldName,
                Type = rule.Type,
                Required = rule.Required,
                Nullable = rule.Nullable
            })
            .ToArray();

        target.ContractVersion = source.ContractVersion;
        target.UpdatedAt = updatedAt;
        target.Rules.Clear();

        foreach (var rule in rules)
            target.Rules.Add(rule);
    }

    /// <summary>
    /// Создаёт транспортный снимок контракта без ссылок
    /// на изменяемые сущности базы.
    /// </summary>
    public static PublishedContract ToDto(this ContractEntity source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        return new PublishedContract
        {
            Slug = source.Slug,
            ContractVersion = source.ContractVersion,
            Rules = source.Rules
                .Select(rule => new ContractRule
                {
                    FieldName = rule.FieldName,
                    Type = rule.Type,
                    Required = rule.Required,
                    Nullable = rule.Nullable
                })
                .ToArray()
        };
    }
}
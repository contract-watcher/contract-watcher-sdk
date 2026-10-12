using Microsoft.AspNetCore.Mvc.Filters;

namespace ContractWatcher.SDK.AspNetCore.Attributes;

/// <summary>
/// Помечает endpoint для автоматической проверки входящего JSON
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ContractWatcherAttribute : Attribute, IFilterMetadata
{
    /// <summary>
    /// Инициализирует новый экземпляр атрибута
    /// </summary>
    /// <param name="contractSlug">Slug контракта, используемого для проверки</param>
    public ContractWatcherAttribute(string contractSlug) 
    {
        if (string.IsNullOrWhiteSpace(contractSlug))
            throw new ArgumentException(nameof(contractSlug));

        ContractSlug = contractSlug;
    }

    /// <summary>
    /// Получает slug контракта
    /// </summary>
    public string ContractSlug { get; }
}
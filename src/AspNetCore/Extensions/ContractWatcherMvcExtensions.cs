using ContractWatcher.SDK.AspNetCore.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.AspNetCore.Extensions;

/// <summary>
/// Содержит методы регистрации ASP.NET Core интеграции ContractWatcher
/// </summary>
public static class ContractWatcherMvcExtensions
{
    /// <summary>
    /// Добавляет автоматическую проверку контрактов через фильтр
    /// </summary>
    /// <param name="builder">MVC builder приложения</param>
    /// <returns>Возвращает исходный MVC builder</returns>
    public static IMvcBuilder AddContractWatcherFilter(this IMvcBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.AddScoped<ContractWatcherResourceFilter>();

        builder.AddMvcOptions(options =>
        {
            options.Filters.AddService<ContractWatcherResourceFilter>();
        });

        return builder;
    }
}
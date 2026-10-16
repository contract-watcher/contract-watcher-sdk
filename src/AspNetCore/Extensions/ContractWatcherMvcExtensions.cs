using ContractWatcher.SDK.AspNetCore.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.AspNetCore.Extensions;

/// <summary>
/// Содержит методы подключения ContractWatcher к ASP.NET Core MVC
/// </summary>
public static class ContractWatcherMvcExtensions
{
    /// <summary>
    /// Добавляет автоматическую проверку endpoint-контрактов, отмеченных ContractWatcherAttribute
    /// </summary>
    public static IMvcBuilder AddContractWatcher(this IMvcBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.TryAddScoped<ContractWatcherResourceFilter>();
        builder.AddMvcOptions(options => options.Filters.AddService<ContractWatcherResourceFilter>());

        return builder;
    }
}
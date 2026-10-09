using ContractWatcher.SDK.Validation.Builders;
using ContractWatcher.SDK.Validation.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Validation.Extensions;

/// <summary>
/// Содержит методы регистрации ContractWatcher
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет стандартный конвейер проверки ContractWatcher
    /// </summary>
    /// <param name="services"> Коллекция сервисов приложения </param>
    /// <param name="configure"> Дополнительная настройка конвейера проверки </param>
    /// <returns>Коллекция сервисов приложения</returns>
    public static IServiceCollection AddContractValidation(this IServiceCollection services, Action<ValidationPipelineBuilder>? configure = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        var builder = new ValidationPipelineBuilder(services);

        builder.UseDefaultRules();

        configure?.Invoke(builder);

        services.TryAddTransient<IValidationPipeline, ValidationPipeline>();
        services.TryAddTransient<IContractValidator, ContractValidator>();

        return services;
    }
}
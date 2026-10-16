using ContractWatcher.SDK.Validation.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Validation.Builders;

/// <summary>
/// Настройка модуля валидации контрактов
/// </summary>
public sealed class ValidationModuleBuilder
{
    private readonly ValidationPipelineBuilder _pipelineBuilder;

    public IServiceCollection Services { get; }

    internal ValidationModuleBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));

        Services.TryAddTransient<IValidationPipeline, ValidationPipeline>();
        Services.TryAddTransient<IContractValidator, ContractValidator>();

        _pipelineBuilder = new ValidationPipelineBuilder(Services);
    }

    /// <summary>
    /// Добавляет стандартные правила
    /// </summary>
    public ValidationModuleBuilder AddDefaultRules()
    {
        _pipelineBuilder.UseDefaultRules();

        return this;
    }

    /// <summary>
    /// Позволяет вручную настроить validation pipeline
    /// </summary>
    public ValidationModuleBuilder Configure(Action<ValidationPipelineBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(_pipelineBuilder);

        return this;
    }

    /// <summary>
    /// Использует пользовательскую реализацию validation pipeline
    /// </summary>
    public ValidationModuleBuilder UsePipeline<TPipeline>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TPipeline : class, IValidationPipeline
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IValidationPipeline), typeof(TPipeline), lifetime));

        return this;
    }

    /// <summary>
    /// Использует пользовательскую реализацию validator контрактов
    /// </summary>
    public ValidationModuleBuilder UseValidator<TValidator>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TValidator : class, IContractValidator
    {
        Services.Replace(ServiceDescriptor.Describe(typeof(IContractValidator), typeof(TValidator), lifetime));

        return this;
    }
}
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Validation.Builders;

/// <summary>
/// Предоставляет API для настройки конвейера проверки ContractWatcher.
/// </summary>
public sealed class ValidationPipelineBuilder
{
    /// <summary>
    /// Коллекция сервисов приложения
    /// </summary>
    private readonly IServiceCollection _services;

    public ValidationPipelineBuilder(IServiceCollection services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Добавляет правило проверки в конец конвейера
    /// </summary>
    /// <typeparam name="TRule">Тип добавляемого правила</typeparam>
    /// <returns>Текущий экземпляр builder</returns>
    public ValidationPipelineBuilder AddRule<TRule>() where TRule : class, IValidationRule
    {
        _services.TryAddEnumerable(ServiceDescriptor.Transient<IValidationRule, TRule>());

        return this;
    }

    /// <summary>
    /// Удаляет все зарегистрированные правила проверки
    /// </summary>
    /// <remarks>После вызова метода стандартные правила ContractWatcher также будут удалены</remarks>
    /// <returns>Текущий экземпляр builder</returns>
    public ValidationPipelineBuilder ClearRules()
    {
        _services.RemoveAll<IValidationRule>();

        return this;
    }

    /// <summary>
    /// Добавляет стандартный набор правил ContractWatcher
    /// </summary>
    /// <remarks>
    /// Стандартный конвейер выполняется в следующем порядке:
    /// <see cref="RequiredRule"/>, <see cref="NullableRule"/>,  <see cref="TypeRule"/>
    /// </remarks>
    /// <returns>Текущий экземпляр builder</returns>
    public ValidationPipelineBuilder UseDefaultRules()
    {
        return 
            AddRule<RequiredRule>()
            .AddRule<NullableRule>()
            .AddRule<TypeRule>();
    }
}
using ContractWatcher.SDK.Execution.Builders;
using ContractWatcher.SDK.Execution.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Execution.Extensions;

/// <summary>
/// Содержит методы регистрации execution pipeline приложения
/// </summary>
public static class ExecutionServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет execution pipeline приложения и стандартный набор middleware
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения</param>
    /// <param name="configure">Необязательная функция дополнительной настройки pipeline</param>
    /// <returns>Возвращает коллекцию сервисов приложения</returns>
    public static IServiceCollection AddContractWatcherExecution(
        this IServiceCollection services, Action<ExecutionPipelineBuilder>? configure = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        var builder = new ExecutionPipelineBuilder(services);

        builder.UseDefaultMiddlewares();
        configure?.Invoke(builder);
        services.TryAddTransient<IExecutionPipeline, ExecutionPipeline>();

        return services;
    }
}
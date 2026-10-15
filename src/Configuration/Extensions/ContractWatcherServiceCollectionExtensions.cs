using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Contracts.Extensions;
using ContractWatcher.SDK.Core.Extensions;
using ContractWatcher.SDK.Execution.Extensions;
using ContractWatcher.SDK.Reporting.Extensions;
using ContractWatcher.SDK.Validation.Extensions;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Configuration.Extensions;

/// <summary>
/// Содержит методы регистрации ContractWatcher SDK в контейнере зависимостей
/// </summary>
public static class ContractWatcherServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет ContractWatcher SDK и его зависимости в контейнер зависимостей
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения</param>
    /// <param name="configure">Делегат конфигурации SDK</param>
    /// <returns>Возвращает исходную коллекцию сервисов</returns>
    public static IServiceCollection AddContractWatcher(this IServiceCollection services, Action<ContractWatcherOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        services
            .AddOptions<ContractWatcherOptions>()
            .Configure(configure)
            .Validate(static options =>
                    Uri.TryCreate(options.CoreUrl, UriKind.Absolute, out var uri) && 
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && 
                    !string.IsNullOrWhiteSpace(uri.Host),
                $"{nameof(ContractWatcherOptions.CoreUrl)} must be a valid HTTP or HTTPS URL.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.IntegrationKey),
                $"{nameof(ContractWatcherOptions.IntegrationKey)} is required.")
            .Validate(options => options.ContractRefreshInterval > TimeSpan.Zero,
                "ContractRefreshInterval must be greater than zero.")
            .ValidateOnStart();

        services.AddLogging();
        services.AddContractWatcherCore();
        services.AddContractWatcherContracts();
        services.AddContractValidation();
        services.AddContractWatcherReporting();
        services.AddContractWatcherExecution();

        services.TryAddTransient<IContractWatcher, Watcher.ContractWatcher>();

        return services;
    }
}
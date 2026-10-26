using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Data.Extensions;

/// <summary>
/// Регистрирует общую базу, используемую стандартными модулями SDK.
/// </summary>
internal static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет единый контекст и согласованный доступ
    /// к tracked-сущностям.
    /// </summary>
    /// <param name="services">Коллекция сервисов SDK.</param>
    /// <remarks>
    /// Повторные вызовы из модулей контрактов и отчётов
    /// не создают новые контексты.
    /// </remarks>
    internal static IServiceCollection AddContractWatcherPersistence(this IServiceCollection services)
    {
        services
            .AddOptions<ContractWatcherOptions>()
            .Validate(
                options =>
                    options.Database is not null &&
                    !string.IsNullOrWhiteSpace(options.Database.DirectoryPath) &&
                    options.Database.JsonSerializerOptions is not null,
                "Database directory and serializer options are required.")
            .ValidateOnStart();

        services.TryAddSingleton<ContractWatcherDbContext>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<ContractWatcherOptions>>()
                .Value;

            return new ContractWatcherDbContext(options.Database);
        });

        services.TryAddSingleton<DatabaseAccess>();

        return services;
    }
}
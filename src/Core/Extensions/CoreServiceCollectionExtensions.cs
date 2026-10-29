using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Constants;
using ContractWatcher.SDK.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Core.Extensions;

/// <summary>
/// Содержит внутренние методы регистрации интеграции с Core
/// </summary>
internal static class CoreServiceCollectionExtensions
{
    internal static IServiceCollection AddContractWatcherCore(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services
            .AddOptions<ContractWatcherOptions>()
            .Validate(static options =>
                {
                    if (!Uri.TryCreate(options.CoreUrl, UriKind.Absolute, out var uri))
                        return false;

                    return
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                        uri.AbsolutePath == "/" &&
                        string.IsNullOrEmpty(uri.Query) &&
                        string.IsNullOrEmpty(uri.Fragment);
                },
                $"{nameof(ContractWatcherOptions.CoreUrl)} must be a base HTTP or HTTPS URL without a path, query, or fragment.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.IntegrationKey),
                $"{nameof(ContractWatcherOptions.IntegrationKey)} is required.")
            .Validate(options =>
                options.CoreRequestTimeout > TimeSpan.Zero &&
                options.CoreRequestTimeout <= TimeSpan.FromMinutes(10),
            "Core request timeout must be within (0, 10 minutes].")
            .Validate(options =>
                options.MaxCoreResponseBytes > 0 &&
                options.MaxCoreResponseBytes <= int.MaxValue,
            "Core response size limit is invalid.")
            .ValidateOnStart();

        services.AddHttpClient<IContractWatcherCoreClient, ContractWatcherCoreClient>(
            static (serviceProvider, httpClient) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<ContractWatcherOptions>>()
                    .Value;

                httpClient.BaseAddress = new Uri(options.CoreUrl);
                httpClient.Timeout = options.CoreRequestTimeout;
                httpClient.MaxResponseContentBufferSize = options.MaxCoreResponseBytes;
                httpClient.DefaultRequestHeaders.Add(CoreHeaders.ApiKey, options.IntegrationKey);
            });

        return services;
    }
}
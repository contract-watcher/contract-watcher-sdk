using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Configuration.Options;
using Microsoft.Extensions.Hosting;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ConfigurationFixture
{
    public const string CoreUrl = "http://localhost:8080";
    public const string IntegrationKey = "test-integration-key";

    public static void ConfigureOptions(ContractWatcherOptions options)
    {
        options.CoreUrl = CoreUrl;
        options.IntegrationKey = IntegrationKey;
        options.ContractRefreshInterval = TimeSpan.FromMinutes(5);
    }

    public static IHost CreateHost(Action<ContractWatcherOptions>? configure = null) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services
                    .AddContractWatcher(configure ?? ConfigureOptions)
                    .AddDefaults();
            })
            .Build();
}
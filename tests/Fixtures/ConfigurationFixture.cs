using ContractWatcher.MicroDb.Options;
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
        options.Database = new MicroDbOptions
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "cw-sdk-tests", Guid.NewGuid().ToString("N"))
        };

        options.CoreUrl = CoreUrl;
        options.IntegrationKey = IntegrationKey;
        options.ContractRefreshInterval = TimeSpan.FromMinutes(5);
    }

    public static IHost CreateHost(Action<ContractWatcherOptions>? configure = null) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services
                    .AddContractWatcher(options =>
                    {
                        ConfigureOptions(options);
                        configure?.Invoke(options);
                    })
                    .AddDefaults();
            })
            .Build();
}
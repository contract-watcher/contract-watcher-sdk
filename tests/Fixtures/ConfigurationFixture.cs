using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Configuration.Options;
using Microsoft.Extensions.Hosting;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ConfigurationFixture
{
    public const string CoreUrl = "https://localhost:5001";
    public const string IntegrationKey = "integration-key";
    
    public static void ConfigureOptions(ContractWatcherOptions options)
    {
        options.CoreUrl = CoreUrl;
        options.IntegrationKey = IntegrationKey;
    }
    
    public static IHost CreateHost(Action<ContractWatcherOptions>? configure = null)
    {
        return new HostBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices(services =>
            {
                services.AddContractWatcher(configure ?? ConfigureOptions);
            })
            .Build();
    }
}
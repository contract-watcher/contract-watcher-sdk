using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class AspNetCoreIntegrationFixture
{
    public static async Task<IHost> CreateHostAsync(IContractWatcher contractWatcher)
    {
        if (contractWatcher is null)
            throw new ArgumentNullException(nameof(contractWatcher));

        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();

                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(contractWatcher);

                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(ContractWatcherTestController).Assembly)
                        .AddContractWatcherFilter();
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            });

        return await host.StartAsync();
    }
}
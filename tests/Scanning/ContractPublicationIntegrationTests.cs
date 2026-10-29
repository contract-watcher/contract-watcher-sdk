using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Data;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Workers;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContractWatcher.SDK.Tests.Scanning;

public sealed class ContractPublicationIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_WithHttpCore_PublishesAndRecoversAfterRestart(bool startOffline)
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        var state = new FakeCore { Online = !startOffline };

        using var coreHost = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app => app.Run(async context =>
                {
                    if (!state.Online)
                    {
                        context.Response.StatusCode =
                            (int)HttpStatusCode.ServiceUnavailable;

                        return;
                    }

                    var path = context.Request.Path.Value!;

                    if (HttpMethods.IsPost(context.Request.Method) &&
                        path.StartsWith("/api/sdk/contracts/publish/", StringComparison.Ordinal))
                    {
                        var contract = await context.Request.ReadFromJsonAsync<PublishedContract>();

                        Assert.NotNull(contract);
                        Assert.Equal("/api/sdk/contracts/publish/" + contract.Slug, path);

                        state.Contracts[contract.Slug] = contract;

                        Interlocked.Increment(ref state.Posts);

                        context.Response.StatusCode = StatusCodes.Status204NoContent;

                        return;
                    }

                    if (HttpMethods.IsGet(context.Request.Method) &&
                        path.StartsWith("/api/sdk/contracts/", StringComparison.Ordinal))
                    {
                        var slug = Uri.UnescapeDataString(path["/api/sdk/contracts/".Length..]);

                        if (state.Contracts.TryGetValue(slug, out var contract))
                            await context.Response.WriteAsJsonAsync(contract);
                        else
                            context.Response.StatusCode = StatusCodes.Status404NotFound;

                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                })))
            .Build();

        await coreHost.StartAsync();

        var core = coreHost.GetTestServer();

        using (var first = CreateHost(fixture, core))
        {
            // Act
            await StartAndWait(first);

            // Assert
            Assert.Equal(1, first.Services.GetRequiredService<ScanCounter>().Count);
            Assert.Equal(startOffline ? 0 : 2, state.Posts);

            if (startOffline)
            {
                var db = first.Services.GetRequiredService<ContractWatcherDbContext>();

                Assert.Equal(2, db.ContractRetries.Count());
            }

            await first.StopAsync();
        }

        // Приложение запускается заново с прежней директорией MicroDb.
        using (var second = CreateHost(fixture, core))
        {
            await StartAndWait(second);

            state.Online = true;

            fixture.Clock.Advance(TimeSpan.FromSeconds(3));

            await second.Services
                .GetRequiredService<ContractDeliveryService>()
                .RunOnceAsync(default);

            Assert.Equal(2, state.Posts);
            Assert.Equal(1, second.Services.GetRequiredService<ScanCounter>().Count);

            var db = second.Services.GetRequiredService<ContractWatcherDbContext>();

            Assert.Empty(db.ContractRetries);
            Assert.All(db.ScannedContracts, entity => Assert.False(entity.Pending));
            Assert.Equal(2, db.Contracts.Count());

            await second.StopAsync();
        }

        using var reopened = fixture.Open();

        Assert.Equal(2, reopened.Contracts.GetSlugs().Count);

        Assert.All(state.Contracts.Values, contract =>
        {
            Assert.Equal(1, contract.ContractVersion);
            Assert.Equal(5, contract.Rules.Count);
            Assert.Contains(contract.Rules, rule => rule.FieldName == "display_name" && rule.Required);
            Assert.Contains(contract.Rules, rule => rule.FieldName == "address" && rule.Nullable);
            Assert.DoesNotContain(contract.Rules, rule => rule.FieldName == "secret");
        });
    }

    private static IHost CreateHost(
        MicroDbFixture fixture,
        TestServer core) =>
        new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(fixture.Clock);
                    services.AddSingleton<ScanCounter>();

                    services
                        .AddContractWatcher(options =>
                        {
                            options.CoreUrl = "http://core.test";
                            options.IntegrationKey = "integration-test";

                            options.Database = new MicroDb.Options.MicroDbOptions 
                                { DirectoryPath = fixture.DirectoryPath };
                        })
                        .AddCore()
                        .AddContracts(contracts => contracts
                            .UseMicroDbStore()
                            .UseCoreRefresher());

                    services
                        .AddHttpClient<IContractWatcherCoreClient, ContractWatcherCoreClient>()
                        .ConfigurePrimaryHttpMessageHandler(core.CreateHandler);

                    services
                        .AddControllers()
                        .ConfigureApplicationPartManager(manager =>
                        {
                            manager.FeatureProviders.Clear();
                            manager.FeatureProviders.Add(new ScanControllerFeatureProvider());
                        })
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
                        })
                        .AddContractWatcherScanning(scanning => scanning
                            .AddSynchronization(options =>
                            {
                                options.SourcePreference = ContractSourcePreference.Application;
                            })
                            .AddPublication(options =>
                            {
                                options.PollInterval = TimeSpan.FromHours(1);
                            })
                            .AddModule<CountingModule>());
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();

    private static async Task StartAndWait(IHost host)
    {
        await host.StartAsync();

        var worker = Assert.Single(host.Services
            .GetServices<IHostedService>()
            .OfType<ContractScanningWorker>());

        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public sealed class ScanCounter
    {
        public int Count;
    }

    public sealed class CountingModule(ScanCounter counter) : IScanningModule
    {
        public ScanningStage Stage => ScanningStage.Custom;

        public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
        {
            counter.Count++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeCore
    {
        public volatile bool Online;

        public int Posts;

        public ConcurrentDictionary<string, PublishedContract> Contracts { get; } = 
            new(StringComparer.Ordinal);
    }

    private sealed class ScanControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) =>
            feature.Controllers.Add(typeof(ScanningFixture.ScanController).GetTypeInfo());
    }
}
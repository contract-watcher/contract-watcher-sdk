using System.Reflection;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Tests.Fixtures;
using ContractWatcher.SDK.Data;
using Moq;
using System.Text.Json.Serialization;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;
// ReSharper disable AccessToDisposedClosure

namespace ContractWatcher.SDK.Tests.Scanning;

public sealed class ContractScannerIntegrationTests
{
    [Fact]
    public async Task Startup_WhenMvcApplicationStarts_DiscoversAndMapsContractsOnce()
    {
        // Arrange
        var probe = new ScanProbe();
        using var fixture = new MicroDbFixture();

        using var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(probe);
                    services.AddSingleton(fixture.Client.Object);
                    services.AddContractWatcher(options => options.Database =
                        new MicroDb.Options.MicroDbOptions { DirectoryPath = fixture.DirectoryPath })
                        .AddContracts(contracts => contracts.UseMicroDbStore().UseCoreRefresher());

                    services.AddControllers()
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
                            .AddSynchronization(options => options.SourcePreference = ContractSourcePreference.Application)
                            .AddModule<CaptureModule>());
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();

        // Act
        await host.StartAsync();

        var contracts = await probe.Completed.Task
            .WaitAsync(TimeSpan.FromSeconds(10));

        var worker = Assert.Single(host.Services
            .GetServices<IHostedService>()
            .OfType<ContractScanningWorker>());

        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(1, probe.Calls);

        Assert.Equal(new[] { "products.explicit", "products.inferred" },
            contracts
                .Select(contract => contract.Slug)
                .OrderBy(slug => slug));

        Assert.All(contracts, contract =>
        {
            var rules = contract.Rules
                .ToDictionary(rule => rule.FieldName);

            Assert.Equal(5, rules.Count);
            Assert.True(rules["display_name"].Required);
            Assert.False(rules["display_name"].Nullable);
            Assert.Equal(ContractFieldType.Number, rules["price"].Type);
            Assert.Equal(ContractFieldType.Object, rules["address"].Type);
            Assert.True(rules["address"].Nullable);
            Assert.Equal(ContractFieldType.Array, rules["tags"].Type);
            Assert.DoesNotContain("secret", rules.Keys);
            Assert.DoesNotContain("city", rules.Keys);
            Assert.DoesNotContain("page", rules.Keys);
            Assert.DoesNotContain("cancellationToken", rules.Keys);
        });

        await host.StopAsync();
    }

    [Fact]
    public async Task Startup_WithSynchronization_ScansOnceAndRestoresPendingContracts()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using (var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(fixture.Client.Object);
                    services.AddContractWatcher(options => options.Database = new MicroDb.Options.MicroDbOptions { DirectoryPath = fixture.DirectoryPath })
                        .AddContracts(contracts => contracts.UseMicroDbStore().UseCoreRefresher());
                    services.AddControllers()
                        .ConfigureApplicationPartManager(manager =>
                        {
                            manager.FeatureProviders.Clear();
                            manager.FeatureProviders.Add(new ScanControllerFeatureProvider());
                        })
                        .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict)
                        .AddContractWatcherScanning(scanning => scanning.AddSynchronization(options =>
                            options.SourcePreference = ContractSourcePreference.Application));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build())
        {
            // Act
            await host.StartAsync();
            var worker = Assert.Single(host.Services.GetServices<IHostedService>().OfType<ContractScanningWorker>());
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
            await host.StopAsync();

            // Assert
            Assert.Equal(2, host.Services.GetRequiredService<ContractWatcherDbContext>().ScannedContracts.Count());
            fixture.Client.Verify(client => client.GetContractAsync("products.explicit", It.IsAny<CancellationToken>()), Times.Once);
            fixture.Client.Verify(client => client.GetContractAsync("products.inferred", It.IsAny<CancellationToken>()), Times.Once);
        }

        using var reopened = fixture.Open();
        Assert.Equal(2, reopened.Context.ScannedContracts.Count());
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        Assert.All(reopened.Context.ScannedContracts, entity =>
        {
            Assert.True(entity.Pending);
            Assert.Equal(0, entity.ExpectedVersion);
        });
    }

    public sealed class ScanProbe
    {
        public int Calls;

        public TaskCompletionSource<IReadOnlyCollection<PublishedContract>> Completed { get; } = 
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class CaptureModule(ScanProbe probe) : IScanningModule
    {
        public ScanningStage Stage => ScanningStage.Custom;

        public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.Calls);
            probe.Completed.TrySetResult(context.Contracts!);

            return Task.CompletedTask;
        }
    }

    private sealed class ScanControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(ScanController).GetTypeInfo());
        }
    }
}
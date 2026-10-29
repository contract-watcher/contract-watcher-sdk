using ContractWatcher.Common.Contracts;
using ContractWatcher.MicroDb.Options;
using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Data.Enums;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Reporting.Publishers;
using ContractWatcher.SDK.Reporting.Workers;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Data.Extensions;

public sealed class PersistenceServiceCollectionExtensionsTests : IDisposable
{
    private readonly MicroDbFixture _fixture = new();

    [Fact]
    public void AddDefaults_RegistersDatabaseAndOneRetryWorker()
    {
        // Arrange
        var services = new ServiceCollection();

        var builder = services
            .AddContractWatcher(options =>
            {
                options.CoreUrl = "https://localhost";
                options.IntegrationKey = "test";
                options.Database = new MicroDbOptions { DirectoryPath = _fixture.DirectoryPath };
            })
            .AddDefaults();

        builder.AddDefaults();

        // Act
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        // Assert
        Assert.IsType<MicroDbContractStore>(provider.GetRequiredService<IContractStore>());
        Assert.IsType<CoreReportPublisher>(provider.GetRequiredService<IReportPublisher>());
        Assert.Single(provider.GetServices<IHostedService>().OfType<ReportRetryWorker>());
    }

    [Fact]
    public async Task AddDefaults_WhenCoreUnavailable_PersistsReportAndSeparateRetryRecord()
    {
        // Arrange
        var report = CreateReport();

        _fixture.Client
            .Setup(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.NetworkError, "Offline"));

        var services = new ServiceCollection();

        services
            .AddContractWatcher(options =>
            {
                options.CoreUrl = "https://localhost";
                options.IntegrationKey = "test";
                options.Database = new MicroDbOptions { DirectoryPath = _fixture.DirectoryPath };
            })
            .AddDefaults();

        services.AddSingleton(_fixture.Client.Object);

        using (var provider = services.BuildServiceProvider())
        {
            // Act
            await provider
                .GetRequiredService<IReportPublisher>()
                .PublishAsync(report);
        }

        // Assert
        // Открываем файлы заново после уничтожения первого контекста.
        using var reopened = _fixture.Open();

        Assert.True(File.Exists(Path.Combine(_fixture.DirectoryPath, "reports.json")));
        Assert.True(File.Exists(Path.Combine(_fixture.DirectoryPath, "report-retries.json")));
        Assert.Equal(ReportDeliveryState.Pending, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);

        var retry = reopened.Context.ReportRetries.Find(report.ReportId)!;

        Assert.Equal(1, retry.FailedAttempts);
        Assert.Equal("Offline", retry.LastError);
        Assert.NotNull(retry.NextAttemptAt);
    }
    
    [Theory]
    [InlineData("database-null")]
    [InlineData("path-empty")]
    [InlineData("serializer-null")]
    public void AddDefaults_WhenDatabaseOptionsAreInvalid_RejectsOptions(string scenario)
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddContractWatcher(options =>
        {
            ConfigurationFixture.ConfigureOptions(options);

            switch (scenario)
            {
                case "database-null":
                    options.Database = null!;
                    break;

                case "path-empty":
                    options.Database = new MicroDbOptions { DirectoryPath = " " };
                    break;

                case "serializer-null":
                    options.Database = new MicroDbOptions
                    {
                        DirectoryPath = _fixture.DirectoryPath,
                        JsonSerializerOptions = null!
                    };
                    break;
            }
        }).AddDefaults();

        using var provider = services.BuildServiceProvider();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => 
            provider.GetRequiredService<IOptions<ContractWatcher.SDK.Configuration.Options.ContractWatcherOptions>>()
            .Value;

        // Assert
        Assert.Throws<OptionsValidationException>(action);
    }

    public void Dispose() => _fixture.Dispose();
}
using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ConfigurationFixture;

namespace ContractWatcher.SDK.Tests.Configuration.Integration;

public sealed class ContractWatcherDependencyInjectionTests
{
    [Fact]
    public async Task AddContractWatcher_WithValidConfiguration_StartsHostSuccessfully()
    {
        // Arrange
        using var host = CreateHost();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => host.StartAsync();

        // Assert
        await action();
    }

    [Fact]
    public async Task AddContractWatcher_WithValidConfiguration_ResolvesSdkGraph()
    {
        // Arrange
        using var host = CreateHost();
        await host.StartAsync();

        // Act
        var watcher = host.Services.GetRequiredService<IContractWatcher>();
        var contractStore = host.Services.GetRequiredService<IContractStore>();
        var contractProvider = host.Services.GetRequiredService<IContractProvider>();
        var validator = host.Services.GetRequiredService<IContractValidator>();
        var reportBuilder = host.Services.GetRequiredService<IValidationReportBuilder>();
        var executionPipeline = host.Services.GetRequiredService<IExecutionPipeline>();

        // Assert
        Assert.NotNull(watcher);
        Assert.NotNull(contractStore);
        Assert.NotNull(contractProvider);
        Assert.NotNull(validator);
        Assert.NotNull(reportBuilder);
        Assert.NotNull(executionPipeline);
    }

    [Fact]
    public async Task AddContractWatcher_WithConfiguration_ProvidesConfiguredOptions()
    {
        // Arrange
        using var host = CreateHost();
        await host.StartAsync();

        // Act
        var options = host.Services.GetRequiredService<IOptions<ContractWatcherOptions>>().Value;

        // Assert
        Assert.Equal(CoreUrl, options.CoreUrl);
        Assert.Equal(IntegrationKey, options.IntegrationKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task AddContractWatcher_WithInvalidIntegrationKey_FailsOnStartup(string integrationKey)
    {
        // Arrange
        using var host =
            CreateHost(options =>
            {
                options.CoreUrl = CoreUrl;
                options.IntegrationKey = integrationKey;
            });

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => host.StartAsync();

        // Assert
        await Assert.ThrowsAsync<OptionsValidationException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-url")]
    [InlineData("/core")]
    [InlineData("ftp://localhost")]
    public async Task AddContractWatcher_WithInvalidCoreUrl_FailsOnStartup(string coreUrl)
    {
        // Arrange
        using var host =
            CreateHost(options =>
            {
                options.CoreUrl = coreUrl;
                options.IntegrationKey = IntegrationKey;
            });

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => host.StartAsync();

        // Assert
        await Assert.ThrowsAsync<OptionsValidationException>(action);
    }
}
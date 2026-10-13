using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Extensions;
using ContractWatcher.SDK.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ConfigurationFixture;

namespace ContractWatcher.SDK.Tests.Core.Extensions;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcherCore_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => CoreServiceCollectionExtensions.AddContractWatcherCore(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcherCore_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddOptions<ContractWatcherOptions>()
            .Configure(options =>
            {
                options.CoreUrl = CoreUrl;
                options.IntegrationKey = IntegrationKey;
            });

        // Act
        var result = services.AddContractWatcherCore();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddContractWatcherCore_RegistersCoreClient()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddOptions<ContractWatcherOptions>()
            .Configure(options =>
            {
                options.CoreUrl = CoreUrl;
                options.IntegrationKey = IntegrationKey;
            });

        services.AddContractWatcherCore();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var client = serviceProvider.GetRequiredService<IContractWatcherCoreClient>();

        // Assert
        Assert.IsType<ContractWatcherCoreClient>(client);
    }
}
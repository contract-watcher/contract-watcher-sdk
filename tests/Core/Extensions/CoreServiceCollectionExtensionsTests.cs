using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Extensions;
using ContractWatcher.SDK.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ConfigurationFixture;

namespace ContractWatcher.SDK.Tests.Core.Extensions;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCore_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ContractWatcherBuilderExtensions.AddCore(null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddCore_ReturnsSameBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        var builder = services.AddContractWatcher(options =>
        {
            options.CoreUrl = CoreUrl;
            options.IntegrationKey = IntegrationKey;
        });

        // Act
        var result = builder.AddCore();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddCore_RegistersCoreClient()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddContractWatcher(options =>
            {
                options.CoreUrl = CoreUrl;
                options.IntegrationKey = IntegrationKey;
            })
            .AddCore();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var client = serviceProvider.GetRequiredService<IContractWatcherCoreClient>();

        // Assert
        Assert.IsType<ContractWatcherCoreClient>(client);
    }
    
    [Fact]
    public void AddContractWatcherCore_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => CoreServiceCollectionExtensions.AddContractWatcherCore(null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcherCore_WithHttpsCoreUrl_ValidatesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddOptions<ContractWatcherOptions>()
            .Configure(options =>
            {
                options.CoreUrl = "https://localhost:8080";
                options.IntegrationKey = IntegrationKey;
            });

        services.AddContractWatcherCore();

        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider
            .GetRequiredService<IOptions<ContractWatcherOptions>>()
            .Value;

        // Assert
        Assert.Equal("https://localhost:8080", options.CoreUrl);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("invalid-url")]
    [InlineData("/core")]
    [InlineData("ftp://localhost")]
    [InlineData("http://localhost:8080/api")]
    [InlineData("http://localhost:8080?test=value")]
    [InlineData("http://localhost:8080#fragment")]
    public async Task AddDefaults_WithInvalidCoreUrl_FailsOnStartup(string coreUrl)
    {
        // Arrange
        using var host = CreateHost(options =>
        {
            options.CoreUrl = coreUrl;
            options.IntegrationKey = IntegrationKey;
            options.ContractRefreshInterval = TimeSpan.FromMinutes(5);
        });

        // Act
        // ReSharper disable once AccessToDisposedClosure
        Func<Task> action = () => host.StartAsync();

        // Assert
        await Assert.ThrowsAsync<OptionsValidationException>(action);
    }
    
    [Theory]
    [InlineData(0, 1024)]
    [InlineData(601, 1024)]
    [InlineData(10, 0)]
    [InlineData(10, 2147483648L)]
    public void AddCore_WhenHttpLimitsAreInvalid_RejectsConfiguration(int seconds, long bytes)
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddContractWatcher(options =>
            {
                options.CoreUrl = CoreUrl;
                options.IntegrationKey = IntegrationKey;
                options.CoreRequestTimeout = TimeSpan.FromSeconds(seconds);
                options.MaxCoreResponseBytes = bytes;
            })
            .AddCore();

        using var provider = services.BuildServiceProvider();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => 
            provider.GetRequiredService<IContractWatcherCoreClient>();

        // Assert
        Assert.Throws<OptionsValidationException>(action);
    }
}
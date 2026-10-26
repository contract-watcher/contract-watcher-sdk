using ContractWatcher.SDK.Scanning.Extensions;
using ContractWatcher.SDK.Scanning.Options;
using ContractWatcher.SDK.Scanning.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Tests.Scanning.Extensions;

public sealed class ScanningServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcherScanning_WhenRepeated_RegistersOneWorker()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherScanning();
        services.AddContractWatcherScanning();

        // Assert
        Assert.Single(services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ContractScanningWorker));
    }

    [Fact]
    public void AddContractWatcherScanning_WhenConfigured_AppliesRetrySettings()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddContractWatcherScanning(builder =>
            builder.ConfigureRetry(options =>
            {
                options.MaxAttempts = 3;
                options.Delay = TimeSpan.FromSeconds(7);
            }));

        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider
            .GetRequiredService<IOptions<ScanningRetryOptions>>()
            .Value;

        // Assert
        Assert.Equal(3, options.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(7), options.Delay);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, -1)]
    [InlineData(5, 86401)]
    public void AddContractWatcherScanning_WhenOptionsAreInvalid_RejectsOptions(int attempts, int seconds)
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddContractWatcherScanning(builder =>
            builder.ConfigureRetry(options =>
            {
                options.MaxAttempts = attempts;
                options.Delay = TimeSpan.FromSeconds(seconds);
            }));

        using var provider = services.BuildServiceProvider();

        // Act
        var action = () => provider
            .GetRequiredService<IOptions<ScanningRetryOptions>>()
            .Value;

        // Assert
        Assert.Throws<OptionsValidationException>(action);
    }

    [Fact]
    public void AddContractWatcherScanning_WhenServicesAreNull_Throws()
    {
        // Act
        var action = () =>
            ScanningServiceCollectionExtensions.AddContractWatcherScanning(null!);

        // Assert
        Assert.Throws<ArgumentNullException>("services", action);
    }
}
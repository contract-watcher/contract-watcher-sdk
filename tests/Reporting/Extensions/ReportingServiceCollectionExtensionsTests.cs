using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Reporting.Extensions;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Reporting.Extensions;

public sealed class ReportingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcherReporting_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => ReportingServiceCollectionExtensions
            .AddContractWatcherReporting(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcherReporting_RegistersTimeProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherReporting();

        // Assert
        using var provider = services.BuildServiceProvider();
        var timeProvider = provider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, timeProvider);
    }

    [Fact]
    public void AddContractWatcherReporting_RegistersHandlersInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherReporting();

        // Assert
        var descriptors = services
            .Where(service => service.ServiceType == typeof(IReportBuildHandler))
            .ToArray();
        
        Assert.Collection(
            descriptors,
            descriptor => Assert.Equal(typeof(ReportMetadataHandler), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(ReportStatusHandler), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(ReportViolationsHandler), descriptor.ImplementationType));
    }

    [Fact]
    public void AddContractWatcherReporting_RegistersValidationReportBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherReporting();

        // Assert
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IValidationReportBuilder));
        
        Assert.Equal(typeof(ValidationReportBuilder), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddContractWatcherReporting_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddContractWatcherReporting();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddContractWatcherReporting_WhenCalledMultipleTimes_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherReporting();
        services.AddContractWatcherReporting();

        // Assert
        var handlers = services
            .Where(service => service.ServiceType == typeof(IReportBuildHandler))
            .ToArray();

        var builders = services
            .Where(service => service.ServiceType == typeof(IValidationReportBuilder))
            .ToArray();

        Assert.Equal(3, handlers.Length);
        Assert.Single(builders);
    }
}
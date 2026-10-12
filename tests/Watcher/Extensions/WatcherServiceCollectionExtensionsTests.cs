using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Watcher.Extensions;
using ContractWatcher.SDK.Watcher.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Watcher.Extensions;

public sealed class WatcherServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcher_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => WatcherServiceCollectionExtensions.AddContractWatcher(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcher_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddContractWatcher();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddContractWatcher_RegistersContractWatcher()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddContractWatcher();
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var watcher = serviceProvider.GetRequiredService<IContractWatcher>();

        // Assert
        Assert.IsType<SDK.Watcher.ContractWatcher>(watcher);
    }

    [Fact]
    public void AddContractWatcher_RegistersRequiredDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddContractWatcher();
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var contractStore = serviceProvider.GetRequiredService<IContractStore>();
        var contractProvider = serviceProvider.GetRequiredService<IContractProvider>();
        var validator = serviceProvider.GetRequiredService<IContractValidator>();
        var reportBuilder = serviceProvider.GetRequiredService<IValidationReportBuilder>();
        var executionPipeline = serviceProvider.GetRequiredService<IExecutionPipeline>();

        // Assert
        Assert.NotNull(contractStore);
        Assert.NotNull(contractProvider);
        Assert.NotNull(validator);
        Assert.NotNull(reportBuilder);
        Assert.NotNull(executionPipeline);
    }

    [Fact]
    public void AddContractWatcher_WhenCalledTwice_DoesNotDuplicateMainServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcher();
        services.AddContractWatcher();

        // Assert
        Assert.Single(services, x => x.ServiceType == typeof(IContractWatcher));
        Assert.Single(services, x => x.ServiceType == typeof(IContractStore));
        Assert.Single(services, x => x.ServiceType == typeof(IContractProvider));
        Assert.Single(services, x => x.ServiceType == typeof(IContractValidator));
        Assert.Single(services, x => x.ServiceType == typeof(IValidationReportBuilder));
        Assert.Single(services, x => x.ServiceType == typeof(IExecutionPipeline));
    }
}
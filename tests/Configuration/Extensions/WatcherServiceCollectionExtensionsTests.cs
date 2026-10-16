using ContractWatcher.SDK.Configuration.Builders;
using ContractWatcher.SDK.Configuration.Extensions;
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

namespace ContractWatcher.SDK.Tests.Configuration.Extensions;

public sealed class ContractWatcherServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcher_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => 
            ContractWatcherServiceCollectionExtensions.AddContractWatcher(null!, ConfigureOptions);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcher_WhenConfigureIsNull_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddContractWatcher());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddContractWatcher_ReturnsBuilderWithSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddContractWatcher(ConfigureOptions);

        // Assert
        Assert.IsType<ContractWatcherBuilder>(builder);
        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddContractWatcher_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddContractWatcher(ConfigureOptions);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider
            .GetRequiredService<IOptions<ContractWatcherOptions>>()
            .Value;

        // Assert
        Assert.Equal(CoreUrl, options.CoreUrl);
        Assert.Equal(IntegrationKey, options.IntegrationKey);
    }

    [Fact]
    public void AddContractWatcher_RegistersContractWatcherDescriptor()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcher(ConfigureOptions);

        // Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IContractWatcher));
    }

    [Fact]
    public void AddContractWatcher_DoesNotRegisterOptionalModules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcher(ConfigureOptions);

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IContractStore));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IContractProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IContractValidator));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidationReportBuilder));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IExecutionPipeline));
    }

    [Fact]
    public void AddDefaults_RegistersRequiredDependencies()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddContractWatcher(ConfigureOptions)
            .AddDefaults();

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
    public void AddContractWatcher_WhenCalledTwice_DoesNotDuplicateRootServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcher(ConfigureOptions);
        services.AddContractWatcher(ConfigureOptions);

        // Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IContractWatcher));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void AddDefaults_RegistersAllMainModules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher(ConfigureOptions)
            .AddDefaults();

        // Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IContractStore));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IContractProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IContractValidator));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IValidationReportBuilder));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IExecutionPipeline));
    }
}
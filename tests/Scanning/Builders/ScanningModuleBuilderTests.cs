using ContractWatcher.SDK.Scanning.Builders;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Scanning.Builders;

public sealed class ScanningModuleBuilderTests
{
    [Fact]
    public void ReplaceModule_WhenModuleExists_PreservesPositionAndOtherModules()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ScanningModuleBuilder(services).UseDefaultModules();

        // Act
        builder.ReplaceModule<SchemaGenerationModule, CustomModule>();

        // Assert
        Assert.Equal(
            new[]
            {
                typeof(EndpointDiscoveryModule),
                typeof(ContractSynchronizationModule),
                typeof(CustomModule),
                typeof(ContractMappingModule),
                typeof(ContractPersistenceModule)
            },
            services
                .Where(value => value.ServiceType == typeof(IScanningModule))
                .Select(value => value.ImplementationType));
    }

    [Fact]
    public void UseDefaultModules_WhenCalledTwice_DoesNotDuplicateModules()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ScanningModuleBuilder(services);

        // Act
        builder.UseDefaultModules().UseDefaultModules();

        // Assert
        Assert.Equal(5, services.Count(value => value.ServiceType == typeof(IScanningModule)));
    }

    [Fact]
    public void ClearModules_WhenRebuilding_RemovesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ScanningModuleBuilder(services).UseDefaultModules();

        // Act
        builder.ClearModules().AddModule<CustomModule>();

        // Assert
        var descriptor = Assert.Single(services, value => 
            value.ServiceType == typeof(IScanningModule));

        Assert.Equal(typeof(CustomModule), descriptor.ImplementationType);
    }
    
    [Fact]
    public void ReplaceModule_WhenCurrentModuleIsMissing_ThrowsWithoutChangingServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);

        var builder = new ScanningModuleBuilder(services)
            .UseDefaultModules();

        var original = services.ToArray();

        // Act
        var action = () => 
            builder.ReplaceModule<CustomModule, SchemaGenerationModule>();

        // Assert
        Assert.Throws<InvalidOperationException>(action);
        Assert.Equal(original, services.ToArray());
    }

    [Fact]
    public void Constructor_WhenServicesAreNull_Throws()
    {
        // Act
        var action = () => new ScanningModuleBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>("services", action);
    }

    [Fact]
    public void ConfigureRetry_WhenCallbackIsNull_Throws()
    {
        // Arrange
        var builder = new ScanningModuleBuilder(new ServiceCollection());

        // Act
        var action = () => builder.ConfigureRetry(null!);

        // Assert
        Assert.Throws<ArgumentNullException>("configure", action);
    }
    
    [Fact]
    public void UseSynchronizationAndPersistence_WhenReplaced_PreservePositions()
    {
        // Arrange
        var services = new ServiceCollection();

        var builder = new ScanningModuleBuilder(services)
            .UseDefaultModules();

        // Act
        builder
            .UseSynchronization<CustomModule>()
            .UsePersistence<CustomModule>();

        // Assert
        Assert.Equal(
            new[]
            {
                typeof(EndpointDiscoveryModule),
                typeof(CustomModule),
                typeof(SchemaGenerationModule),
                typeof(ContractMappingModule),
                typeof(CustomModule)
            },
            services
                .Where(item => item.ServiceType == typeof(IScanningModule))
                .Select(item => item.ImplementationType));
    }

    public sealed class CustomModule : IScanningModule
    {
        public ScanningStage Stage => ScanningStage.Schema;

        public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
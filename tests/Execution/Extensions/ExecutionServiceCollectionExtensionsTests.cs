using ContractWatcher.SDK.Execution;
using ContractWatcher.SDK.Execution.Extensions;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Middlewares;
using ContractWatcher.SDK.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Execution.Extensions;

public sealed class ExecutionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcherExecution_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => 
            ExecutionServiceCollectionExtensions.AddContractWatcherExecution(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcherExecution_RegistersDefaultMiddlewares()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherExecution();

        // Assert
        var descriptors = services
            .Where(service => service.ServiceType == typeof(IWatcherMiddleware))
            .ToArray();

        Assert.Collection(
            descriptors,
            descriptor =>
            {
                Assert.Equal(typeof(ContractResolutionMiddleware), descriptor.ImplementationType);
                Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
            },
            descriptor =>
            {
                Assert.Equal(typeof(ValidationMiddleware), descriptor.ImplementationType);
                Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
            });
    }

    [Fact]
    public void AddContractWatcherExecution_RegistersExecutionPipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherExecution();

        // Assert
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IExecutionPipeline));
        
        Assert.Equal(typeof(ExecutionPipeline), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddContractWatcherExecution_WhenConfigureIsProvided_InvokesConfigure()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureCalled = false;

        // Act
        services.AddContractWatcherExecution(_ => configureCalled = true);

        // Assert
        Assert.True(configureCalled);
    }

    [Fact]
    public void AddContractWatcherExecution_WhenConfigureAddsMiddleware_RegistersMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherExecution(builder => builder.AddMiddleware<FakeMiddleware>());

        // Assert
        var descriptors = services
            .Where(service => service.ServiceType == typeof(IWatcherMiddleware))
            .ToArray();

        Assert.Collection(
            descriptors,
            descriptor => Assert.Equal(typeof(ContractResolutionMiddleware), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(ValidationMiddleware), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(FakeMiddleware), descriptor.ImplementationType));
    }

    [Fact]
    public void AddContractWatcherExecution_WhenConfigureClearsMiddlewares_RemovesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractWatcherExecution(builder =>
        {
            builder
                .ClearMiddlewares()
                .AddMiddleware<FakeMiddleware>();
        });
        
        // Assert
        var descriptors = services
            .Where(service => service.ServiceType == typeof(IWatcherMiddleware))
            .ToArray();

        var descriptor = Assert.Single(descriptors);

        Assert.Equal(typeof(FakeMiddleware), descriptor.ImplementationType);
    }

    [Fact]
    public void AddContractWatcherExecution_WhenConfigureIsNull_RegistersSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddContractWatcherExecution(null);

        // Assert
        var exception = Record.Exception(action);

        Assert.Null(exception);
    }

    [Fact]
    public void AddContractWatcherExecution_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddContractWatcherExecution();

        // Assert
        Assert.Same(services, result);
    }
}
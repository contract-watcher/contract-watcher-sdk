using ContractWatcher.SDK.Execution.Builders;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Middlewares;
using ContractWatcher.SDK.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Execution.Builders;

public sealed class ExecutionPipelineBuilderTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ExecutionPipelineBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddMiddleware_RegistersMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        builder.AddMiddleware<FakeMiddleware>();

        // Assert
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IWatcherMiddleware));
        Assert.Equal(typeof(FakeMiddleware), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddMiddleware_ReturnsSameBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        var result = builder.AddMiddleware<FakeMiddleware>();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddMiddleware_WhenMiddlewareAlreadyRegistered_DoesNotDuplicateRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        builder
            .AddMiddleware<FakeMiddleware>()
            .AddMiddleware<FakeMiddleware>();

        // Assert
        var descriptors = services
            .Where(service => service.ServiceType == typeof(IWatcherMiddleware))
            .ToArray();

        Assert.Single(descriptors);
    }

    [Fact]
    public void ClearMiddlewares_RemovesRegisteredMiddlewares()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        builder
            .AddMiddleware<FakeMiddleware>()
            .AddMiddleware<FakeMiddleware>();

        // Act
        builder.ClearMiddlewares();

        // Assert
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IWatcherMiddleware));
    }

    [Fact]
    public void ClearMiddlewares_ReturnsSameBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        var result = builder.ClearMiddlewares();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void UseDefaultMiddlewares_RegistersDefaultMiddlewares()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        builder.UseDefaultMiddlewares();

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
    public void UseDefaultMiddlewares_ReturnsSameBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionPipelineBuilder(services);

        // Act
        var result = builder.UseDefaultMiddlewares();

        // Assert
        Assert.Same(builder, result);
    }
}
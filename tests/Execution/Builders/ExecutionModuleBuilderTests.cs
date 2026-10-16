using ContractWatcher.SDK.Execution;
using ContractWatcher.SDK.Execution.Builders;
using ContractWatcher.SDK.Execution.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Execution.Builders;

public sealed class ExecutionModuleBuilderTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action action = () => _ = new ExecutionModuleBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Configure_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ExecutionModuleBuilder(new ServiceCollection());

        // Act
        Action action = () => builder.Configure(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Configure_InvokesConfiguration()
    {
        // Arrange
        var builder = new ExecutionModuleBuilder(new ServiceCollection());
        var invoked = false;

        // Act
        builder.Configure(_ => invoked = true);

        // Assert
        Assert.True(invoked);
    }

    [Fact]
    public void UsePipeline_ReplacesExecutionPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ExecutionModuleBuilder(services);

        // Act
        builder.UsePipeline<ExecutionPipeline>(ServiceLifetime.Singleton);

        // Assert
        var descriptor = Assert.Single(services, x => x.ServiceType == typeof(IExecutionPipeline));
        Assert.Equal(typeof(ExecutionPipeline), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
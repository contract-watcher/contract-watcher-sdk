using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.AspNetCore.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.AspNetCore.Extensions;

public sealed class ContractWatcherMvcExtensionsTests
{
    [Fact]
    public void AddContractWatcher_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ContractWatcherMvcExtensions.AddContractWatcher(null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcher_ReturnsSameMvcBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddControllers();

        // Act
        var result = builder.AddContractWatcher();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddContractWatcher_RegistersResourceFilter()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddControllers();

        // Act
        builder.AddContractWatcher();

        // Assert
        Assert.Contains(services, descriptor => 
            descriptor.ServiceType == typeof(ContractWatcherResourceFilter) && 
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddContractWatcher_AddsFilterToMvcOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddControllers()
            .AddContractWatcher();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>()
            .Value;

        // Act
        var registration = options.Filters
            .OfType<ServiceFilterAttribute>()
            .SingleOrDefault(filter => filter.ServiceType == typeof(ContractWatcherResourceFilter));

        // Assert
        Assert.NotNull(registration);
    }
}
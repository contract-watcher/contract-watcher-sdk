using ContractWatcher.SDK.AspNetCore.Extensions;
using ContractWatcher.SDK.AspNetCore.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.AspNetCore.Extensions;

public sealed class ContractWatcherMvcExtensionsTests
{
    [Fact]
    public void AddContractWatcherFilter_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => ContractWatcherMvcExtensions.AddContractWatcherFilter(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddContractWatcherFilter_ReturnsSameMvcBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddControllers();

        // Act
        var result = builder.AddContractWatcherFilter();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddContractWatcherFilter_RegistersResourceFilter()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddControllers();

        // Act
        builder.AddContractWatcherFilter();

        // Assert
        Assert.Contains(services, descriptor => 
            descriptor.ServiceType == typeof(ContractWatcherResourceFilter) && 
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddContractWatcherFilter_AddsFilterToMvcOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        services
            .AddControllers()
            .AddContractWatcherFilter();

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>().Value;

        // Act
        var registration = options.Filters
            .OfType<ServiceFilterAttribute>()
            .SingleOrDefault(x => x.ServiceType == typeof(ContractWatcherResourceFilter));

        // Assert
        Assert.NotNull(registration);
    }
}
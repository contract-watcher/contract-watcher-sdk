using System.Text.Json;
using ContractWatcher.SDK.AspNetCore.Attributes;
using ContractWatcher.SDK.AspNetCore.Filters;
using ContractWatcher.SDK.Watcher.Interfaces;
using ContractWatcher.SDK.Watcher.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.AspNetCoreFixture;

namespace ContractWatcher.SDK.Tests.AspNetCore.Filters;

public sealed class ContractWatcherResourceFilterTests
{
    [Fact]
    public void Constructor_WhenContractWatcherIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();

        // Act
        var action = 
            () => new ContractWatcherResourceFilter(null!, logger.Object);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();

        // Act
        var action = 
            () => new ContractWatcherResourceFilter(contractWatcher.Object, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);

        ResourceExecutionDelegate next = () => Task.FromResult<ResourceExecutedContext>(null!);

        // Act
        var action = () => filter.OnResourceExecutionAsync(null!, next);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WhenNextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);
        var context = CreateResourceContext();

        // Act
        var action = () => filter.OnResourceExecutionAsync(context, null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WhenAttributeDoesNotExist_DoesNotCallWatcher()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);
        var context = CreateResourceContext();
        var nextCalled = false;

        ResourceExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ResourceExecutedContext>(null!);
        };

        // Act
        await filter.OnResourceExecutionAsync(context, next);

        // Assert
        contractWatcher.Verify(x => 
            x.ValidateAsync(It.IsAny<string>(), 
                It.IsAny<JsonElement>(), 
                It.IsAny<CancellationToken>()), 
            Times.Never);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WhenAttributeExists_CallsWatcher()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();

        contractWatcher.Setup(x => 
                x.ValidateAsync("products", 
                    It.IsAny<JsonElement>(), 
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ContractWatchResult>());

        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);

        var context = CreateResourceContext(""" { "id": "123" } """,
                new ContractWatcherAttribute("products"));

        ResourceExecutionDelegate next = () => Task.FromResult<ResourceExecutedContext>(null!);

        // Act
        await filter.OnResourceExecutionAsync(context, next);

        // Assert
        contractWatcher.Verify(x => x.ValidateAsync("products", 
                It.IsAny<JsonElement>(), 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task OnResourceExecutionAsync_WhenEndpointDoesNotExist_DoesNotCallWatcher()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);
        var context = CreateResourceContext();
        var nextCalled = false;
        
        context.HttpContext.SetEndpoint(null);
        
        ResourceExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ResourceExecutedContext>(null!);
        };

        // Act
        await filter.OnResourceExecutionAsync(context, next);

        // Assert
        contractWatcher.Verify(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.True(nextCalled);
    }
    
    [Fact]
    public async Task OnResourceExecutionAsync_WhenBodyContainsInvalidJson_DoesNotInterruptPipeline()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        var logger = new Mock<ILogger<ContractWatcherResourceFilter>>();
        var filter = new ContractWatcherResourceFilter(contractWatcher.Object, logger.Object);
        var nextCalled = false;

        var context = CreateResourceContext(""" { "id": } """,
                new ContractWatcherAttribute("products"));

        ResourceExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ResourceExecutedContext>(null!);
        };

        // Act
        await filter.OnResourceExecutionAsync(context, next);

        // Assert
        contractWatcher.Verify(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.True(nextCalled);
    }
}
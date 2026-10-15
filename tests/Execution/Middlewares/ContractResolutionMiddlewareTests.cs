using System.Net;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Middlewares;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ExecutionFixture;

namespace ContractWatcher.SDK.Tests.Execution.Middlewares;

public sealed class ContractResolutionMiddlewareTests
{
    [Fact]
    public void Constructor_WhenContractProviderIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ContractResolutionMiddleware(
            null!, NullLogger<ContractResolutionMiddleware>.Instance);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractExists_SetsContract()
    {
        // Arrange
        var contract = CreateContract();
        var provider = new Mock<IContractProvider>();

        provider
            .Setup(x => x.GetAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Same(contract, context.Contract);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractExists_CallsNext()
    {
        // Arrange
        var contract = CreateContract();
        var provider = new Mock<IContractProvider>();

        provider
            .Setup(x => x.GetAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();
        var nextCalled = false;

        WatcherDelegate next = (_, _) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractDoesNotExist_SetsContractUnavailable()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();

        provider
            .Setup(x => x.GetAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedContract?)null);

        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();
        
        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.ContractUnavailable, context.Status);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractDoesNotExist_DoesNotSetContract()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();

        provider
            .Setup(x => x.GetAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedContract?)null);

        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();
        
        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Null(context.Contract);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractDoesNotExist_DoesNotCallNext()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();
        
        provider
            .Setup(x => x.GetAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedContract?)null);
        
        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();
        var nextCalled = false;

        WatcherDelegate next = (_, _) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PassesSlugAndCancellationTokenToProvider()
    {
        // Arrange
        var contract = CreateContract();
        var provider = new Mock<IContractProvider>();
        using var cancellationTokenSource = new CancellationTokenSource();
        
        // ReSharper disable once AccessToDisposedClosure
        provider
            .Setup(x => x.GetAsync("products", cancellationTokenSource.Token))
            .ReturnsAsync(contract);

        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();
        
        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, cancellationTokenSource.Token);

        // Assert
        // ReSharper disable once AccessToDisposedClosure
        provider.Verify(x => x.GetAsync("products", cancellationTokenSource.Token), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();
        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        
        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var action = () => middleware.InvokeAsync(null!, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();
        var middleware = new ContractResolutionMiddleware(
            provider.Object, NullLogger<ContractResolutionMiddleware>.Instance);
        var context = CreateContext();

        // Act
        var action = () => middleware.InvokeAsync(context, null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }
    
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new Mock<IContractProvider>();

        // Act
        var action = () => new ContractResolutionMiddleware(provider.Object, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenCoreRequestFails_SetsContractUnavailableAndStopsPipeline()
    {
        // Arrange
        var provider =
            new Mock<IContractProvider>();

        provider
            .Setup(x => x.GetAsync(
                "products",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new CoreRequestException(
                    CoreRequestError.ServerError,
                    "Core unavailable.",
                    HttpStatusCode.InternalServerError));

        var logger = new Mock<ILogger<ContractResolutionMiddleware>>();
        var middleware = new ContractResolutionMiddleware(provider.Object, logger.Object);
        var context = CreateContext(contractSlug: "products");
        var nextCalled = false;

        WatcherDelegate next = (_, _) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.ContractUnavailable, context.Status);
        Assert.False(nextCalled);
    }
}
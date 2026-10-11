using ContractWatcher.SDK.Execution;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Tests.Fakes;

namespace ContractWatcher.SDK.Tests.Execution;

public sealed class ExecutionPipelineTests
{
    [Fact]
    public void Constructor_WhenMiddlewaresIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ExecutionPipeline(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = new ExecutionPipeline([]);

        // Act
        var action = () => pipeline.ExecuteAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewaresAreRegistered_ExecutesInRegistrationOrder()
    {
        // Arrange
        var calls = new List<int>();

        var middlewares = new IWatcherMiddleware[]
        {
            new FakeMiddleware(
                async (context, next, cancellationToken) =>
                {
                    calls.Add(1);
                    await next(context, cancellationToken);
                }),

            new FakeMiddleware(
                async (context, next, cancellationToken) =>
                {
                    calls.Add(2);
                    await next(context, cancellationToken);
                }),

            new FakeMiddleware(
                async (context, next, cancellationToken) =>
                {
                    calls.Add(3);
                    await next(context, cancellationToken);
                })
        };

        var pipeline = new ExecutionPipeline(middlewares);
        var context = CreateContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal([1, 2, 3], calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareDoesNotCallNext_StopsPipeline()
    {
        // Arrange
        var calls = new List<int>();

        var middlewares = new IWatcherMiddleware[]
        {
            new FakeMiddleware(
                async (context, next, cancellationToken) =>
                {
                    calls.Add(1);
                    await next(context, cancellationToken);
                }),

            new FakeMiddleware(
                (_, _, _) =>
                {
                    calls.Add(2);
                    return Task.CompletedTask;
                }),

            new FakeMiddleware(
                async (context, next, cancellationToken) =>
                {
                    calls.Add(3);
                    await next(context, cancellationToken);
                })
        };

        var pipeline = new ExecutionPipeline(middlewares);
        var context = CreateContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal([1, 2], calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMiddlewaresRegistered_CompletesSuccessfully()
    {
        // Arrange
        var pipeline = new ExecutionPipeline([]);
        var context = CreateContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public async Task ExecuteAsync_PassesSameContextThroughPipeline()
    {
        // Arrange
        WatcherContext? receivedContext = null;

        var middleware = new FakeMiddleware(
            async (context, next, cancellationToken) =>
            {
                receivedContext = context;
                await next(context, cancellationToken);
            });

        var pipeline = new ExecutionPipeline([middleware]);
        var context = CreateContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Same(context, receivedContext);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenThroughPipeline()
    {
        // Arrange
        CancellationToken receivedToken = default;

        var middleware = new FakeMiddleware(
            async (context, next, cancellationToken) =>
            {
                receivedToken = cancellationToken;
                await next(context, cancellationToken);
            });

        var pipeline = new ExecutionPipeline([middleware]);
        var context = CreateContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await pipeline.ExecuteAsync(context, cancellationTokenSource.Token);

        // Assert
        Assert.Equal(cancellationTokenSource.Token, receivedToken);
    }

    private static WatcherContext CreateContext() =>
        new()
        {
            ContractSlug = "products",
            Payload = default
        };
}
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Middlewares;
using Microsoft.Extensions.Logging.Abstractions;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ExecutionFixture;

namespace ContractWatcher.SDK.Tests.Execution.Middlewares;

public sealed class TechnicalErrorMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var middleware = CreateMiddleware();

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var act = () => middleware.InvokeAsync(null!, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateContext();

        // Act
        var act = () => middleware.InvokeAsync(context, null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextCompletes_DoesNotChangeStatus()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateContext();

        WatcherDelegate next = (ctx, _) =>
        {
            ctx.Status = WatcherStatus.Failed;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.Failed, context.Status);
    }

    [Fact]
    public async Task InvokeAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateContext();

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        WatcherDelegate next = (_, token) => throw new OperationCanceledException(token);

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var act = () => middleware.InvokeAsync(context, next, cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
        Assert.NotEqual(WatcherStatus.TechnicalError, context.Status);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedExceptionOccurs_SetsTechnicalError()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = CreateContext();

        WatcherDelegate next = (_, _) => throw new InvalidOperationException("Unexpected error.");

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.TechnicalError, context.Status);
    }

    private static TechnicalErrorMiddleware CreateMiddleware() =>
        new (NullLogger<TechnicalErrorMiddleware>.Instance);
}
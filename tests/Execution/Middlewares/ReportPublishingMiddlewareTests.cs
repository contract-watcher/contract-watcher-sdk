using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Middlewares;
using ContractWatcher.SDK.Reporting.Interfaces;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;
using static ContractWatcher.SDK.Tests.Fixtures.WatcherFixture;

namespace ContractWatcher.SDK.Tests.Execution.Middlewares;

public sealed class ReportPublishingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var publisher = new Mock<IReportPublisher>();
        var middleware = new ReportPublishingMiddleware(publisher.Object);

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var act = () => middleware.InvokeAsync(null!, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task InvokeAsync_WithNullNext_ThrowsArgumentNullException()
    {
        // Arrange
        var publisher = new Mock<IReportPublisher>();
        var middleware = new ReportPublishingMiddleware(publisher.Object);
        var context = CreateContext();

        // Act
        var act = () => middleware.InvokeAsync(context, null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task InvokeAsync_WithoutReport_ThrowsInvalidOperationException()
    {
        // Arrange
        var publisher = new Mock<IReportPublisher>();
        var middleware = new ReportPublishingMiddleware(publisher.Object);
        var context = CreateContext();

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var act = () => middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);

        publisher.Verify(x => 
            x.PublishAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithReport_PublishesReportAndCallsNext()
    {
        // Arrange
        var publisher = new Mock<IReportPublisher>();
        var middleware = new ReportPublishingMiddleware(publisher.Object);
        var report = CreateReport();
        var context = CreateContext();
        context.Report = report;

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var nextCalled = false;

        WatcherDelegate next = (_, token) =>
        {
            nextCalled = true;
            Assert.Equal(cancellationToken, token);

            return Task.CompletedTask;
        };

        publisher
            .Setup(x => x.PublishAsync(report, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, next, cancellationToken);

        // Assert
        publisher.Verify(x => x.PublishAsync(report, cancellationToken), Times.Once);

        Assert.True(nextCalled);
    }
}
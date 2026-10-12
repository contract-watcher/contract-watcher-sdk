using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Middlewares;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Tests.Fakes;
using ContractWatcher.SDK.Validation.Models;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ExecutionFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ValidationFixture;

namespace ContractWatcher.SDK.Tests.Execution.Middlewares;

public sealed class ReportMiddlewareTests
{
    [Fact]
    public void Constructor_WhenReportBuilderIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ReportMiddleware(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);

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
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);
        var context = CreateContext();

        // Act
        var action = () => middleware.InvokeAsync(context, null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);

        var context = new WatcherContext
        {
            ContractSlug = "products",
            Payload = default,
            ValidationResult = CreateValidationResult()
        };

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var action = () => middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationResultIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);

        var context = new WatcherContext
        {
            ContractSlug = "products",
            Payload = default,
            Contract = CreateContract()
        };

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var action = () => middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task InvokeAsync_BuildsReport()
    {
        // Arrange
        var report = CreateReport();
        var builder = new FakeValidationReportBuilder { Result = report };
        var middleware = new ReportMiddleware(builder);
        var context = CreateValidatedContext();

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Same(report, context.Report);
    }

    [Fact]
    public async Task InvokeAsync_PassesContractAndValidationResultToBuilder()
    {
        // Arrange
        PublishedContract? receivedContract = null;
        ContractValidationResult? receivedValidationResult = null;

        var builder =
            new FakeValidationReportBuilder
            {
                OnBuild = (contract, validationResult) =>
                {
                    receivedContract = contract;
                    receivedValidationResult = validationResult;
                }
            };

        var middleware = new ReportMiddleware(builder);
        var context = CreateValidatedContext();

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Same(context.Contract, receivedContract);
        Assert.Same(context.ValidationResult, receivedValidationResult);
    }

    [Fact]
    public async Task InvokeAsync_WhenReportBuilt_CallsNext()
    {
        // Arrange
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);
        var context = CreateValidatedContext();
        var nextCalled = false;

        WatcherDelegate next = (_, _) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PassesCancellationTokenToNext()
    {
        // Arrange
        var builder = new FakeValidationReportBuilder();
        var middleware = new ReportMiddleware(builder);
        var context = CreateValidatedContext();
        using var cancellationTokenSource = new CancellationTokenSource();

        CancellationToken receivedToken = default;

        WatcherDelegate next = (_, cancellationToken) =>
        {
            receivedToken = cancellationToken;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, cancellationTokenSource.Token);

        // Assert
        Assert.Equal(cancellationTokenSource.Token, receivedToken);
    }
}
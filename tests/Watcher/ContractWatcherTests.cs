using System.Text.Json;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ValidationFixture;

namespace ContractWatcher.SDK.Tests.Watcher;

public sealed class ContractWatcherTests
{
    [Fact]
    public void Constructor_WhenExecutionPipelineIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new SDK.Watcher.ContractWatcher(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ValidateAsync_WhenContractSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? contractSlug)
    {
        // Arrange
        var pipeline = new Mock<IExecutionPipeline>();
        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        var action = () => watcher.ValidateAsync(contractSlug!, default);

        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task ValidateAsync_PassesContextToExecutionPipeline()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" { "id": "123" } """);
        var payload = document.RootElement;

        WatcherContext? receivedContext = null;

        var pipeline = new Mock<IExecutionPipeline>();

        pipeline
            .Setup(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), It.IsAny<CancellationToken>()))
            .Callback<WatcherContext, CancellationToken>(
                (context, _) =>
                {
                    receivedContext = context;
                    context.Status = WatcherStatus.Passed;
                    context.ValidationResult = CreateValidationResult();
                })
            .Returns(Task.CompletedTask);

        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        await watcher.ValidateAsync("products", payload);

        // Assert
        Assert.NotNull(receivedContext);
        Assert.Equal("products", receivedContext.ContractSlug);
        Assert.Equal(payload.GetRawText(), receivedContext.Payload.GetRawText());
    }

    [Fact]
    public async Task ValidateAsync_PassesCancellationTokenToExecutionPipeline()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var pipeline = new Mock<IExecutionPipeline>();

        pipeline
            .Setup(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), cancellationToken))
            .Callback<WatcherContext, CancellationToken>(
                (context, _) =>
                {
                    context.Status = WatcherStatus.Passed;
                    context.ValidationResult = CreateValidationResult();
                })
            .Returns(Task.CompletedTask);

        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        await watcher.ValidateAsync("products", default, cancellationToken);

        // Assert
        pipeline.Verify(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WhenValidationPasses_ReturnsPassed()
    {
        // Arrange
        var pipeline = new Mock<IExecutionPipeline>();

        pipeline
            .Setup(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), It.IsAny<CancellationToken>()))
            .Callback<WatcherContext, CancellationToken>(
                (context, _) =>
                {
                    context.Status = WatcherStatus.Passed;
                    context.ValidationResult = CreateValidationResult();
                })
            .Returns(Task.CompletedTask);

        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        var result = await watcher.ValidateAsync("products", default);

        // Assert
        Assert.Equal(WatcherStatus.Passed, result.Status);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task ValidateAsync_WhenValidationFails_ReturnsViolations()
    {
        // Arrange
        var violation = CreateViolation();
        var validationResult = CreateValidationResult([violation]);
        var pipeline = new Mock<IExecutionPipeline>();

        pipeline
            .Setup(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), It.IsAny<CancellationToken>()))
            .Callback<WatcherContext, CancellationToken>(
                (context, _) =>
                {
                    context.Status = WatcherStatus.Failed;
                    context.ValidationResult = validationResult;
                })
            .Returns(Task.CompletedTask);

        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        var result = await watcher.ValidateAsync("products", default);

        // Assert
        Assert.Equal(WatcherStatus.Failed, result.Status);
        Assert.Same(validationResult.Violations, result.Violations);
    }

    [Fact]
    public async Task ValidateAsync_WhenContractIsUnavailable_ReturnsContractUnavailable()
    {
        // Arrange
        var pipeline = new Mock<IExecutionPipeline>();

        pipeline
            .Setup(x => x.ExecuteAsync(It.IsAny<WatcherContext>(), It.IsAny<CancellationToken>()))
            .Callback<WatcherContext, CancellationToken>(
                (context, _) =>
                {
                    context.Status = WatcherStatus.ContractUnavailable;
                })
            .Returns(Task.CompletedTask);

        var watcher = new SDK.Watcher.ContractWatcher(pipeline.Object);

        // Act
        var result = await watcher.ValidateAsync("products", default);

        // Assert
        Assert.Equal(WatcherStatus.ContractUnavailable, result.Status);
        Assert.Empty(result.Violations);
    }
}
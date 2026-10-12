using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Execution.Middlewares;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ExecutionFixture;

namespace ContractWatcher.SDK.Tests.Execution.Middlewares;

public sealed class ValidationMiddlewareTests
{
    [Fact]
    public void Constructor_WhenValidatorIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ValidationMiddleware(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationSucceeds_SetsPassedStatus()
    {
        // Arrange
        var validationResult = new ContractValidationResult { Violations = [] };
        var validator = new Mock<IContractValidator>();
        var context = CreateValidatedContext();

        validator
            .Setup(x => x.Validate(context.Payload, context.Contract!))
            .Returns(validationResult);

        var middleware = new ValidationMiddleware(validator.Object);

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.Passed, context.Status);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationFails_SetsFailedStatus()
    {
        // Arrange
        var violation = new ValidationViolation { FieldName = "id", Type = default };
        var validationResult = new ContractValidationResult { Violations = [violation] };
        var validator = new Mock<IContractValidator>();
        var context = CreateValidatedContext();

        validator
            .Setup(x => x.Validate(context.Payload, context.Contract!))
            .Returns(validationResult);

        var middleware = new ValidationMiddleware(validator.Object);

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Equal(WatcherStatus.Failed, context.Status);
    }

    [Fact]
    public async Task InvokeAsync_SetsValidationResult()
    {
        // Arrange
        var validationResult = new ContractValidationResult { Violations = [] };
        var validator = new Mock<IContractValidator>();
        var context = CreateValidatedContext();

        validator
            .Setup(x => x.Validate(context.Payload, context.Contract!))
            .Returns(validationResult);

        var middleware = new ValidationMiddleware(validator.Object);

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.Same(validationResult, context.ValidationResult);
    }

    [Fact]
    public async Task InvokeAsync_WhenContractIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var validator = new Mock<IContractValidator>();
        var middleware = new ValidationMiddleware(validator.Object);
        var context = new WatcherContext { ContractSlug = "products", Payload = default };

        WatcherDelegate next = (_, _) => Task.CompletedTask;

        // Act
        var action = () => middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationCompletes_CallsNext()
    {
        // Arrange
        var validationResult = new ContractValidationResult { Violations = [] };
        var validator = new Mock<IContractValidator>();
        var context = CreateValidatedContext();

        validator
            .Setup(x => x.Validate(context.Payload, context.Contract!))
            .Returns(validationResult);

        var middleware = new ValidationMiddleware(validator.Object);
        var nextCalled = false;

        WatcherDelegate next = (_, _) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await middleware.InvokeAsync(context, next, CancellationToken.None);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new Mock<IContractValidator>();
        var middleware = new ValidationMiddleware(validator.Object);

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
        var validator = new Mock<IContractValidator>();
        var middleware = new ValidationMiddleware(validator.Object);
        var context = CreateContext();

        // Act
        var action = () => middleware.InvokeAsync(context, null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }
}
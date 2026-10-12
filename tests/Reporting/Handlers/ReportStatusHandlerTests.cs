using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Handlers;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Reporting.Handlers;

public sealed class ReportStatusHandlerTests
{
    [Fact]
    public void Handle_WhenValidationPassed_SetsPassedStatus()
    {
        // Arrange
        var handler = new ReportStatusHandler();
        var context = CreateReportBuildContext(violations: []);

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.Equal(ValidationStatus.Passed, context.Status);
    }

    [Fact]
    public void Handle_WhenValidationPassed_DoesNotCallNext()
    {
        // Arrange
        var handler = new ReportStatusHandler();
        var context = CreateReportBuildContext(violations: []);
        var nextCalled = false;

        ReportBuildDelegate next = _ => { nextCalled = true; };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.False(nextCalled);
    }

    [Fact]
    public void Handle_WhenValidationFailed_SetsFailedStatus()
    {
        // Arrange
        var handler = new ReportStatusHandler();
        
        var violation = new ValidationViolation
        {
            FieldName = "id",
            Type = ViolationType.RequiredFieldMissing
        };

        var context = CreateReportBuildContext(violations: [violation]);

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.Equal(ValidationStatus.Failed, context.Status);
    }

    [Fact]
    public void Handle_WhenValidationFailed_CallsNext()
    {
        // Arrange
        var handler = new ReportStatusHandler();

        var violation = new ValidationViolation
        {
            FieldName = "id",
            Type = ViolationType.RequiredFieldMissing
        };

        var context = CreateReportBuildContext(violations: [violation]);
        var nextCalled = false;

        ReportBuildDelegate next = _ => { nextCalled = true; };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public void Handle_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ReportStatusHandler();

        ReportBuildDelegate next = _ => { };

        // Act
        var action = () => handler.Handle(null!, next);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Handle_WhenNextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ReportStatusHandler();
        var context = CreateReportBuildContext(violations: []);

        // Act
        var action = () => handler.Handle(context, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
}
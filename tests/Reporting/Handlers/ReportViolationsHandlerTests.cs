using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Handlers;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Reporting.Handlers;

public sealed class ReportViolationsHandlerTests
{
    [Fact]
    public void Handle_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ReportViolationsHandler();

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
        var handler = new ReportViolationsHandler();
        var context = CreateReportBuildContext();

        // Act
        var action = () => handler.Handle(context, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Handle_CopiesValidationViolationsToReportContext()
    {
        // Arrange
        var violation = new ValidationViolation
        {
            FieldName = "id",
            Type = ViolationType.RequiredFieldMissing
        };

        var context = CreateReportBuildContext([violation]);
        var handler = new ReportViolationsHandler();

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.Same(context.ValidationResult.Violations, context.Violations);
    }

    [Fact]
    public void Handle_WhenMultipleViolationsExist_CopiesAllViolations()
    {
        // Arrange
        var violations = new ValidationViolation[]
        {
            new()
            {
                FieldName = "id",
                Type = ViolationType.RequiredFieldMissing
            },
            new()
            {
                FieldName = "name",
                Type = ViolationType.NullNotAllowed
            }
        };

        var context = CreateReportBuildContext(violations);
        var handler = new ReportViolationsHandler();

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.Equal(violations, context.Violations);
    }

    [Fact]
    public void Handle_CallsNext()
    {
        // Arrange
        var context = CreateReportBuildContext();
        var handler = new ReportViolationsHandler();
        var nextCalled = false;

        ReportBuildDelegate next = _ => { nextCalled = true; };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.True(nextCalled);
    }
}
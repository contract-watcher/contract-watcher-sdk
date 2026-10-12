using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Reporting.Builders;

public sealed class ValidationReportBuilderTests
{
    [Fact]
    public void Constructor_WhenHandlersIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ValidationReportBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Build_WhenContractIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var action = () => builder.Build(null!, validationResult);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Build_WhenValidationResultIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();
        var contract = CreateContract();

        // Act
        var action = () => builder.Build(contract, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Build_WhenValidationPassed_GeneratesReportId()
    {
        // Arrange
        var builder = CreateBuilder();
        var contract = CreateContract();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var report = builder.Build(contract, validationResult);

        // Assert
        Assert.NotEqual(Guid.Empty, report.ReportId);
    }

    [Fact]
    public void Build_WhenCalledMultipleTimes_GeneratesUniqueReportIds()
    {
        // Arrange
        var builder = CreateBuilder();
        var contract = CreateContract();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var firstReport = builder.Build(contract, validationResult);
        var secondReport = builder.Build(contract, validationResult);

        // Assert
        Assert.NotEqual(firstReport.ReportId, secondReport.ReportId);
    }

    [Fact]
    public void Build_WhenValidationPassed_UsesContractVersion()
    {
        // Arrange
        const int expectedVersion = 7;
        var builder = CreateBuilder();

        var contract = new PublishedContract
        {
            Slug = "products",
            ContractVersion = expectedVersion,
            Rules = []
        };

        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var report = builder.Build(contract, validationResult);

        // Assert
        Assert.Equal(expectedVersion, report.ContractVersion);
    }

    [Fact]
    public void Build_WhenValidationPassed_SetsPassedStatus()
    {
        // Arrange
        var builder = CreateBuilder();
        var contract = CreateContract();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var report = builder.Build(contract, validationResult);

        // Assert
        Assert.Equal(ValidationStatus.Passed, report.Status);
    }

    [Fact]
    public void Build_WhenValidationPassed_ReturnsEmptyViolations()
    {
        // Arrange
        var builder = CreateBuilder();
        var contract = CreateContract();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var report = builder.Build(contract, validationResult);

        // Assert
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Build_WhenNoHandlerSetsStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new ValidationReportBuilder([]);
        var contract = CreateContract();
        var validationResult = CreateSuccessfulValidationResult();

        // Act
        var action = () => builder.Build(contract, validationResult);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    private static ValidationReportBuilder CreateBuilder()
    {
        IReportBuildHandler[] handlers =
        [
            new ReportMetadataHandler(TimeProvider.System),
            new ReportStatusHandler()
        ];

        return new ValidationReportBuilder(handlers);
    }

    private static PublishedContract CreateContract() =>
        new()
        {
            Slug = "products",
            ContractVersion = 1,
            Rules = []
        };

    private static ContractValidationResult CreateSuccessfulValidationResult() => new() { Violations = [] };
}
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Models;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ReportingFixture
{
    public static ValidationReport CreateReport(
        int contractVersion = 1,
        ValidationStatus status = ValidationStatus.Passed,
        IReadOnlyCollection<ValidationViolation>? violations = null) =>
        new()
        {
            Slug = "test-contract",
            ReportId = Guid.NewGuid(),
            ContractVersion = contractVersion,
            Status = status,
            Violations = violations ?? []
        };
    
    public static ReportBuildContext CreateReportBuildContext(IReadOnlyCollection<ValidationViolation>? violations = null) =>
        new()
        {
            Contract = new PublishedContract
            {
                Slug = "products",
                ContractVersion = 1,
                Rules = []
            },
            ValidationResult = new ContractValidationResult { Violations = violations ?? [] }
        };
}
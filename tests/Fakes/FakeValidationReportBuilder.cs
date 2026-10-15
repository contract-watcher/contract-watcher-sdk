using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class FakeValidationReportBuilder : IValidationReportBuilder
{
    public ValidationReport Result { get; init; } = CreateReport();

    public Action<PublishedContract, ContractValidationResult>? OnBuild { get; init; }

    public ValidationReport Build(PublishedContract contract, ContractValidationResult validationResult)
    {
        OnBuild?.Invoke(contract, validationResult);

        return Result;
    }
    
    private static ValidationReport CreateReport() =>
        new()
        {
            Slug = "test-contract",
            ReportId = Guid.NewGuid(),
            ContractVersion = 1,
            Status = ValidationStatus.Passed,
            Violations = []
        };
}
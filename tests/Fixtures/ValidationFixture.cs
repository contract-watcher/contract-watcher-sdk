using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Reporting.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ValidationFixture
{
    public static ContractValidationResult CreateValidationResult(
        IReadOnlyCollection<ValidationViolation>? violations = null) =>
        new() { Violations = violations ?? [] };

    public static ValidationViolation CreateViolation(
        string fieldName = "id",
        ViolationType type = ViolationType.RequiredFieldMissing,
        ContractFieldType? expectedType = null,
        ContractFieldType? actualType = null) =>
        new()
        {
            FieldName = fieldName,
            Type = type,
            ExpectedType = expectedType,
            ActualType = actualType
        };
    
    public static ValidationReportBuilder CreateValidationReportBuilder()
    {
        IReportBuildHandler[] handlers =
        [
            new ReportMetadataHandler(TimeProvider.System),
            new ReportStatusHandler()
        ];

        return new ValidationReportBuilder(handlers);
    }
    
    public static ValidationContext CreateValidationContext(
        JsonElement? value, 
        bool nullable = true, 
        bool required = true, 
        ContractFieldType type = ContractFieldType.String) =>
        new()
        {
            Rule = new ContractRule
            {
                FieldName = "customer_id",
                Type = type,
                Required = required,
                Nullable = nullable
            },
            Value = value
        };
}
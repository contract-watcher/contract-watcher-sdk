using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ContractFixture
{
    public static PublishedContract CreateContract(
        string slug = "products", int version = 1, IReadOnlyCollection<ContractRule>? rules = null) =>
        new()
        {
            Slug = slug,
            ContractVersion = version,
            Rules = rules ??
            [
                new()
                {
                    FieldName = "id",
                    Type = ContractFieldType.String,
                    Required = true,
                    Nullable = false
                }
            ]
        };
    
    public static ContractValidationResult CreateSuccessfulValidationResult() => new() { Violations = [] };
}
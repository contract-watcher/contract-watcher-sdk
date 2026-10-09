using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Validation;

public sealed class ContractValidationResultTests
{
    [Fact]
    public void IsValid_WhenViolationsAreEmpty_ReturnsTrue()
    {
        // Arrange
        var result = new ContractValidationResult { Violations = [] };

        // Act
        var isValid = result.IsValid;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WhenViolationsExist_ReturnsFalse()
    {
        // Arrange
        var result = new ContractValidationResult
        {
            Violations =
            [
                new ValidationViolation
                {
                    FieldName = "customer_id",
                    Type = ViolationType.RequiredFieldMissing,
                    ExpectedType = ContractFieldType.String
                }
            ]
        };

        // Act
        var isValid = result.IsValid;

        // Assert
        Assert.False(isValid);
    }
}
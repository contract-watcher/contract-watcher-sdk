using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;
using ContractWatcher.SDK.Validation.Rules;

namespace ContractWatcher.SDK.Tests.Rules;

public sealed class RequiredRuleTests
{
    private readonly RequiredRule _rule = new();

    [Fact]
    public void CanValidate_WhenFieldIsRequired_ReturnsTrue()
    {
        // Arrange
        var context = CreateContext(required: true, value: null);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanValidate_WhenFieldIsOptional_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(required: false, value: null);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_WhenRequiredFieldIsMissing_ReturnsViolation()
    {
        // Arrange
        var context = CreateContext(required: true, value: null);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("customer_id", result.FieldName);
        Assert.Equal(ViolationType.RequiredFieldMissing, result.Type);
        Assert.Equal(ContractFieldType.String, result.ExpectedType);
        Assert.Null(result.ActualType);
    }

    [Fact]
    public void Validate_WhenRequiredFieldExists_ReturnsNull()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" "customer-1" """);
        var context = CreateContext(required: true, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Validate_WhenRequiredFieldContainsNull_ReturnsNull()
    {
        // Arrange
        using var document = JsonDocument.Parse("null");
        var context = CreateContext(required: true, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void Validate_WhenFieldIsOptional_ReturnsNull()
    {
        // Arrange
        var context = CreateContext(required: false, value: null);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }

    private static ValidationContext CreateContext(bool required, JsonElement? value)
    {
        return new ValidationContext
        {
            Rule = new ContractRule
            {
                FieldName = "customer_id",
                Type = ContractFieldType.String,
                Required = required,
                Nullable = false
            },
            Value = value
        };
    }
}
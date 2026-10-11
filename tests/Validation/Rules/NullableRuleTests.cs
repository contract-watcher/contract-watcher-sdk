using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;
using ContractWatcher.SDK.Validation.Rules;

namespace ContractWatcher.SDK.Tests.Rules;

public sealed class NullableRuleTests
{
    private readonly NullableRule _rule = new();

    [Fact]
    public void CanValidate_WhenFieldIsMissing_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(nullable: false, value: null);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanValidate_WhenFieldExists_ReturnsTrue()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" "customer-1" """);
        var context = CreateContext(nullable: false, value: document.RootElement);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Validate_WhenNullableFieldContainsNull_ReturnsNull()
    {
        // Arrange
        using var document = JsonDocument.Parse("""null""");
        var context = CreateContext(nullable: true, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Validate_WhenNonNullableFieldContainsNull_ReturnsViolation()
    {
        // Arrange
        using var document = JsonDocument.Parse("null");
        var context = CreateContext(nullable: false, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("customer_id", result.FieldName);
        Assert.Equal(ViolationType.NullNotAllowed, result.Type);
        Assert.Equal(ContractFieldType.String, result.ExpectedType);
        Assert.Null(result.ActualType);
    }

    [Fact]
    public void Validate_WhenNonNullableFieldContainsValue_ReturnsNull()
    {
        // Arrange
        using var document = JsonDocument.Parse("\"customer-1\"");
        var context = CreateContext(nullable: false, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void Validate_WhenFieldIsMissing_ReturnsNull()
    {
        // Arrange
        var context = CreateContext(nullable: false, value: null);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }

    private static ValidationContext CreateContext(bool nullable, JsonElement? value)
    {
        return new ValidationContext
        {
            Rule = new ContractRule
            {
                FieldName = "customer_id",
                Type = ContractFieldType.String,
                Required = true,
                Nullable = nullable
            },
            Value = value
        };
    }
}
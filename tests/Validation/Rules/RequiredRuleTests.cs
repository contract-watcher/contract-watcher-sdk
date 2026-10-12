using System.Text.Json;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Rules;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ValidationFixture;

namespace ContractWatcher.SDK.Tests.Validation.Rules;

public sealed class RequiredRuleTests
{
    private readonly RequiredRule _rule = new();

    [Fact]
    public void CanValidate_WhenFieldIsRequired_ReturnsTrue()
    {
        // Arrange
        var context = CreateValidationContext(required: true, value: null);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanValidate_WhenFieldIsOptional_ReturnsFalse()
    {
        // Arrange
        var context = CreateValidationContext(required: false, value: null);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_WhenRequiredFieldIsMissing_ReturnsViolation()
    {
        // Arrange
        var context = CreateValidationContext(required: true, value: null);

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
        var context = CreateValidationContext(required: true, value: document.RootElement);

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
        var context = CreateValidationContext(required: true, value: document.RootElement);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void Validate_WhenFieldIsOptional_ReturnsNull()
    {
        // Arrange
        var context = CreateValidationContext(required: false, value: null);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }
}
using System.Text.Json;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Rules;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ValidationFixture;

namespace ContractWatcher.SDK.Tests.Validation.Rules;

public sealed class TypeRuleTests
{
    private readonly TypeRule _rule = new();

    [Fact]
    public void CanValidate_WhenFieldIsMissing_ReturnsFalse()
    {
        // Arrange
        var context = CreateValidationContext(null, type: ContractFieldType.String);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanValidate_WhenFieldContainsNull_ReturnsFalse()
    {
        // Arrange
        using var document = JsonDocument.Parse("null");
        var context = CreateValidationContext(document.RootElement, type: ContractFieldType.String);

        // Act
        var result = _rule.CanValidate(context);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(ContractFieldType.String, """ "value" """)]
    [InlineData(ContractFieldType.Number, "42")]
    [InlineData(ContractFieldType.Number, "42.5")]
    [InlineData(ContractFieldType.Boolean, "true")]
    [InlineData(ContractFieldType.Boolean, "false")]
    [InlineData(ContractFieldType.Object, """ { "id" : 1 } """)]
    [InlineData(ContractFieldType.Array, """ [1, 2, 3] """)]
    public void Validate_WhenTypeMatches_ReturnsNull(ContractFieldType expectedType, string json)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);
        var context = CreateValidationContext(document.RootElement, type: expectedType);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(ContractFieldType.String, "42", ContractFieldType.Number)]
    [InlineData(ContractFieldType.Number, """ "42" """, ContractFieldType.String)]
    [InlineData(ContractFieldType.Boolean, """ "true" """, ContractFieldType.String)]
    [InlineData(ContractFieldType.Object, "[]", ContractFieldType.Array)]
    [InlineData(ContractFieldType.Array, "{}", ContractFieldType.Object)]
    public void Validate_WhenTypeDoesNotMatch_ReturnsViolation(ContractFieldType expectedType, string json, ContractFieldType actualType)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);
        var context = CreateValidationContext(document.RootElement, type: expectedType);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("customer_id", result.FieldName);
        Assert.Equal(ViolationType.TypeMismatch, result.Type);
        Assert.Equal(expectedType, result.ExpectedType);
        Assert.Equal(actualType, result.ActualType);
    }
    
    [Fact]
    public void Validate_WhenContractFieldTypeIsUnsupported_ReturnsViolation()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" "value" """);
        var context = CreateValidationContext(document.RootElement, type: (ContractFieldType)999);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ViolationType.TypeMismatch, result.Type);
    }
    
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Validate_WhenActualTypeIsBoolean_ReturnsBooleanActualType(string json)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);
        var context = CreateValidationContext(document.RootElement, type: ContractFieldType.String);

        // Act
        var result = _rule.Validate(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ContractFieldType.Boolean, result.ActualType);
    }
    
    [Fact]
    public void Validate_WhenJsonValueKindIsUnsupported_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var context = CreateValidationContext(default(JsonElement), type: ContractFieldType.String);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _rule.Validate(context));
    }
}
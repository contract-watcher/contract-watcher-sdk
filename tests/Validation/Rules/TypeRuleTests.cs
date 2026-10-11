using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Validation.Models;
using ContractWatcher.SDK.Validation.Rules;

namespace ContractWatcher.SDK.Tests.Rules;

public sealed class TypeRuleTests
{
    private readonly TypeRule _rule = new();

    [Fact]
    public void CanValidate_WhenFieldIsMissing_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(ContractFieldType.String, value: null);

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
        var context = CreateContext(ContractFieldType.String, document.RootElement);

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
        var context = CreateContext(expectedType, document.RootElement);

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
        var context = CreateContext(expectedType, document.RootElement);

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
        var context = CreateContext((ContractFieldType)999, document.RootElement);

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
        var context = CreateContext(ContractFieldType.String, document.RootElement);

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
        var context = CreateContext(ContractFieldType.String, default(JsonElement));

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _rule.Validate(context));
    }

    private static ValidationContext CreateContext(ContractFieldType type, JsonElement? value)
    {
        return new ValidationContext
        {
            Rule = new ContractRule
            {
                FieldName = "customer_id",
                Type = type,
                Required = true,
                Nullable = false
            },
            Value = value
        };
    }
}
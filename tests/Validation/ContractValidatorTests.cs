using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Tests.Fakes;
using ContractWatcher.SDK.Validation;

namespace ContractWatcher.SDK.Tests.Validation;

public sealed class ContractValidatorTests
{
    [Fact]
    public void Constructor_WhenPipelineIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        FakeValidationPipeline? pipeline = null;

        // Act
        var action = () => new ContractValidator(pipeline!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Validate_WhenContractIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var document = JsonDocument.Parse("{}");
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);

        // Act
        var action = () => validator.Validate(document.RootElement, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Validate_WhenRootElementIsNotObject_ThrowsArgumentException()
    {
        // Arrange
        using var document = JsonDocument.Parse("[]");
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract();

        // Act
        var action = () => validator.Validate(document.RootElement, contract);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Validate_WhenFieldExists_PassesFieldValueToPipeline()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" { "customer_id": "customer-1" } """);
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract(CreateRule("customer_id"));

        // Act
        validator.Validate(document.RootElement, contract);

        // Assert
        var context = Assert.Single(pipeline.Contexts);
        Assert.Equal("customer_id", context.Rule.FieldName);
        Assert.NotNull(context.Value);
        Assert.Equal(JsonValueKind.String, context.Value.Value.ValueKind);
        Assert.Equal("customer-1", context.Value.Value.GetString());
    }

    [Fact]
    public void Validate_WhenFieldIsMissing_PassesNullValueToPipeline()
    {
        // Arrange
        using var document = JsonDocument.Parse("{}");
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract(CreateRule("customer_id"));

        // Act
        validator.Validate(document.RootElement, contract);

        // Assert
        var context = Assert.Single(pipeline.Contexts);
        Assert.Equal("customer_id", context.Rule.FieldName);
        Assert.Null(context.Value);
    }

    [Fact]
    public void Validate_WhenFieldContainsJsonNull_PassesJsonNullToPipeline()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" { "customer_id": null } """);
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract(CreateRule("customer_id"));

        // Act
        validator.Validate(document.RootElement, contract);

        // Assert
        var context = Assert.Single(pipeline.Contexts);
        Assert.NotNull(context.Value);
        Assert.Equal(JsonValueKind.Null, context.Value.Value.ValueKind);
    }

    [Fact]
    public void Validate_WhenContractContainsMultipleRules_ValidatesEachRule()
    {
        // Arrange
        using var document = JsonDocument.Parse(""" { "customer_id": "customer-1", "amount": 100 } """);
        var pipeline = new FakeValidationPipeline();
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract(
            CreateRule("customer_id", ContractFieldType.String),
            CreateRule("amount", ContractFieldType.Number));

        // Act
        validator.Validate(document.RootElement, contract);

        // Assert
        Assert.Equal(2, pipeline.Contexts.Count);
        Assert.Equal("customer_id", pipeline.Contexts[0].Rule.FieldName);
        Assert.Equal("amount", pipeline.Contexts[1].Rule.FieldName);
    }

    [Fact]
    public void Validate_WhenPipelineReturnsViolations_ReturnsAllViolations()
    {
        // Arrange
        using var document = JsonDocument.Parse("{}");
        
        var violation = new ValidationViolation
        {
            FieldName = "customer_id",
            Type = ViolationType.RequiredFieldMissing,
            ExpectedType = ContractFieldType.String
        };
        
        var pipeline = new FakeValidationPipeline([violation]);
        var validator = new ContractValidator(pipeline);
        var contract = CreateContract(CreateRule("customer_id"));

        // Act
        var result = validator.Validate(document.RootElement, contract);

        // Assert
        var actualViolation = Assert.Single(result.Violations);
        Assert.Same(violation, actualViolation);
    }

    private static PublishedContract CreateContract(params ContractRule[] rules) =>
        new()
        {
            Slug = "products",
            ContractVersion = 1,
            Rules = rules
        };

    private static ContractRule CreateRule(string fieldName, ContractFieldType type = ContractFieldType.String) =>
        new()
        {
            FieldName = fieldName,
            Type = type,
            Required = true,
            Nullable = false
        };
}
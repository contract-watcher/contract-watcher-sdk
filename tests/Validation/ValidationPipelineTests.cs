using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Tests.Fakes;
using ContractWatcher.SDK.Validation;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Validation;

public sealed class ValidationPipelineTests
{
    [Fact]
    public void Constructor_WhenRulesAreNull_ThrowsArgumentNullException()
    {
        // Arrange
        IEnumerable<IValidationRule>? rules = null;

        // Act
        var action = () => new ValidationPipeline(rules!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Validate_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = new ValidationPipeline([]);

        // Act
        var action = () => pipeline.Validate(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Validate_WhenRuleCannotValidate_SkipsRule()
    {
        // Arrange
        var executed = false;
        var rule = new FakeValidationRule(canValidate: false, onValidate: () => executed = true);
        var pipeline = new ValidationPipeline([rule]);
        var context = CreateContext();

        // Act
        var result = pipeline.Validate(context);

        // Assert
        Assert.False(executed);
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_WhenRuleReturnsNull_DoesNotAddViolation()
    {
        // Arrange
        var rule = new FakeValidationRule(canValidate: true, violation: null);
        var pipeline = new ValidationPipeline([rule]);
        var context = CreateContext();

        // Act
        var result = pipeline.Validate(context);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_WhenRuleReturnsViolation_AddsViolation()
    {
        // Arrange
        var violation = CreateViolation();
        var rule = new FakeValidationRule(violation: violation);
        var pipeline = new ValidationPipeline([rule]);
        var context = CreateContext();

        // Act
        var result = pipeline.Validate(context);

        // Assert
        Assert.Single(result);
        Assert.Same(violation, result.Single());
    }

    [Fact]
    public void Validate_WhenMultipleRulesReturnViolations_ReturnsAllViolations()
    {
        // Arrange
        var firstViolation = CreateViolation("first");
        var secondViolation = CreateViolation("second");
        var pipeline = new ValidationPipeline(
        [
            new FakeValidationRule(violation: firstViolation),
            new FakeValidationRule(violation: secondViolation)
        ]);
        var context = CreateContext();

        // Act
        var result = pipeline.Validate(context);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal([firstViolation, secondViolation], result);
    }

    [Fact]
    public void Validate_ExecutesRulesInRegistrationOrder()
    {
        // Arrange
        var calls = new List<int>();
        var pipeline = new ValidationPipeline(
        [
            new FakeValidationRule(onValidate: () => calls.Add(1)),
            new FakeValidationRule(onValidate: () => calls.Add(2)),
            new FakeValidationRule(onValidate: () => calls.Add(3))
        ]);
        var context = CreateContext();

        // Act
        pipeline.Validate(context);

        // Assert
        Assert.Equal([1, 2, 3], calls);
    }

    private static ValidationContext CreateContext() =>
        new()
        {
            Rule = new ContractRule
            {
                FieldName = "customer_id",
                Type = ContractFieldType.String,
                Required = true,
                Nullable = false
            }
        };

    private static ValidationViolation CreateViolation(string fieldName = "customer_id") =>
        new()
        {
            FieldName = fieldName,
            Type = ViolationType.RequiredFieldMissing,
            ExpectedType = ContractFieldType.String
        };
}
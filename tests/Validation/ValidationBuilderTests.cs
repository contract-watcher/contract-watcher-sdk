using ContractWatcher.SDK.Tests.Fakes;
using ContractWatcher.SDK.Validation;
using ContractWatcher.SDK.Validation.Builders;
using ContractWatcher.SDK.Validation.Extensions;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Validation;

public sealed class ValidationBuilderTests
{
    [Fact]
    public void AddContractValidation_RegistersDefaultRules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation();
        using var provider = services.BuildServiceProvider();
        var rules = provider.GetServices<IValidationRule>().ToArray();

        // Assert
        Assert.Collection(
            rules,
            rule => Assert.IsType<RequiredRule>(rule),
            rule => Assert.IsType<NullableRule>(rule),
            rule => Assert.IsType<TypeRule>(rule));
    }
    
    [Fact]
    public void AddContractValidation_RegistersValidationPipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation();
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IValidationPipeline>();

        // Assert
        Assert.IsType<ValidationPipeline>(pipeline);
    }
    
    [Fact]
    public void AddContractValidation_RegistersContractValidator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IContractValidator>();

        // Assert
        Assert.IsType<ContractValidator>(validator);
    }
    
    [Fact]
    public void AddContractValidation_WhenCustomRuleAdded_AppendsRuleAfterDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation(builder =>
        {
            builder.AddRule<CustomValidationRule>();
        });
        using var provider = services.BuildServiceProvider();
        var rules = provider.GetServices<IValidationRule>().ToArray();

        // Assert
        Assert.Collection(
            rules,
            rule => Assert.IsType<RequiredRule>(rule),
            rule => Assert.IsType<NullableRule>(rule),
            rule => Assert.IsType<TypeRule>(rule),
            rule => Assert.IsType<CustomValidationRule>(rule));
    }
    
    [Fact]
    public void AddContractValidation_WhenRulesCleared_RemovesDefaultRules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation(builder =>
        {
            builder.ClearRules().AddRule<CustomValidationRule>();
        });
        using var provider = services.BuildServiceProvider();
        var rules = provider.GetServices<IValidationRule>().ToArray();
        
        // Assert
        var rule = Assert.Single(rules);
        Assert.IsType<CustomValidationRule>(rule);
    }
    
    [Fact]
    public void AddContractValidation_WhenDefaultRulesRestored_RegistersDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContractValidation(builder =>
        {
            builder.ClearRules().UseDefaultRules();
        });
        using var provider = services.BuildServiceProvider();
        var rules = provider.GetServices<IValidationRule>().ToArray();

        // Assert
        Assert.Collection(
            rules,
            rule => Assert.IsType<RequiredRule>(rule),
            rule => Assert.IsType<NullableRule>(rule),
            rule => Assert.IsType<TypeRule>(rule));
    }
    
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act
        var action = () => new ValidationPipelineBuilder(services!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
}
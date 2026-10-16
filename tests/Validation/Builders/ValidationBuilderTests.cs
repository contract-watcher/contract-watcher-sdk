using ContractWatcher.SDK.Configuration.Extensions;
using ContractWatcher.SDK.Tests.Fakes;
using ContractWatcher.SDK.Validation;
using ContractWatcher.SDK.Validation.Builders;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Validation.Builders;

public sealed class ValidationBuilderTests
{
    [Fact]
    public void AddDefaultValidation_RegistersDefaultRules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddDefaultValidation();

        using var provider = services.BuildServiceProvider();

        var rules = provider
            .GetServices<IValidationRule>()
            .ToArray();

        // Assert
        Assert.Collection(
            rules,
            rule => Assert.IsType<RequiredRule>(rule),
            rule => Assert.IsType<NullableRule>(rule),
            rule => Assert.IsType<TypeRule>(rule));
    }

    [Fact]
    public void AddValidation_RegistersValidationPipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation => validation.AddDefaultRules());

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IValidationPipeline>();

        // Assert
        Assert.IsType<ValidationPipeline>(pipeline);
    }

    [Fact]
    public void AddValidation_RegistersContractValidator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation => validation.AddDefaultRules());

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IContractValidator>();

        // Assert
        Assert.IsType<ContractValidator>(validator);
    }

    [Fact]
    public void AddValidation_WhenCustomRuleAdded_AppendsRuleAfterDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation =>
            {
                validation
                    .AddDefaultRules()
                    .Configure(pipeline => pipeline.AddRule<CustomValidationRule>());
            });

        using var provider = services.BuildServiceProvider();

        var rules = provider
            .GetServices<IValidationRule>()
            .ToArray();

        // Assert
        Assert.Collection(
            rules,
            rule => Assert.IsType<RequiredRule>(rule),
            rule => Assert.IsType<NullableRule>(rule),
            rule => Assert.IsType<TypeRule>(rule),
            rule => Assert.IsType<CustomValidationRule>(rule));
    }

    [Fact]
    public void AddValidation_WhenOnlyCustomRuleConfigured_DoesNotRegisterDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation =>
            {
                validation.Configure(pipeline => pipeline.AddRule<CustomValidationRule>());
            });

        using var provider = services.BuildServiceProvider();

        var rules = provider
            .GetServices<IValidationRule>()
            .ToArray();

        // Assert
        var rule = Assert.Single(rules);

        Assert.IsType<CustomValidationRule>(rule);
    }

    [Fact]
    public void AddValidation_WhenRulesCleared_RemovesDefaultRules()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation =>
            {
                validation
                    .AddDefaultRules()
                    .Configure(pipeline =>
                    {
                        pipeline
                            .ClearRules()
                            .AddRule<CustomValidationRule>();
                    });
            });

        using var provider = services.BuildServiceProvider();

        var rules = provider
            .GetServices<IValidationRule>()
            .ToArray();

        // Assert
        var rule = Assert.Single(rules);

        Assert.IsType<CustomValidationRule>(rule);
    }

    [Fact]
    public void AddValidation_WhenDefaultRulesRestored_RegistersDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddContractWatcher()
            .AddValidation(validation =>
            {
                validation.Configure(pipeline =>
                {
                    pipeline
                        .AddRule<CustomValidationRule>()
                        .ClearRules()
                        .UseDefaultRules();
                });
            });

        using var provider = services.BuildServiceProvider();

        var rules = provider
            .GetServices<IValidationRule>()
            .ToArray();

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
    
    [Fact]
    public void ValidationModuleBuilder_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action action = () =>
        {
            _ = new ValidationModuleBuilder(null!);
        };

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Configure_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ValidationModuleBuilder(
            new ServiceCollection());

        // Act
        Action action = () =>
        {
            builder.Configure(null!);
        };

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void UsePipeline_ReplacesValidationPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ValidationModuleBuilder(services);

        // Act
        builder.UsePipeline<ValidationPipeline>(
            ServiceLifetime.Singleton);

        // Assert
        var descriptor = Assert.Single(
            services,
            x => x.ServiceType == typeof(IValidationPipeline));

        Assert.Equal(
            typeof(ValidationPipeline),
            descriptor.ImplementationType);

        Assert.Equal(
            ServiceLifetime.Singleton,
            descriptor.Lifetime);
    }

    [Fact]
    public void UseValidator_ReplacesContractValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ValidationModuleBuilder(services);

        // Act
        builder.UseValidator<ContractValidator>(ServiceLifetime.Scoped);

        // Assert
        var descriptor = Assert.Single(services, x => x.ServiceType == typeof(IContractValidator));
        Assert.Equal(typeof(ContractValidator), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
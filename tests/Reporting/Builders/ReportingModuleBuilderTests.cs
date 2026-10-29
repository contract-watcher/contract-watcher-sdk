using ContractWatcher.SDK.Reporting.Builders;
using ContractWatcher.SDK.Reporting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Tests.Reporting.Builders;

public sealed class ReportingModuleBuilderTests
{
    [Fact]
    public void UseCorePublisher_WhenConfigured_AppliesRetryOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ReportingModuleBuilder(services);

        // Act
        var result = builder.UseCorePublisher(options =>
        {
            options.PollInterval = TimeSpan.FromSeconds(7);
            options.InitialDelay = TimeSpan.FromSeconds(3);
            options.MaxDelay = TimeSpan.FromMinutes(2);
            options.MaxAttempts = 5;
            options.BatchSize = 12;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptions<ReportRetryOptions>>()
            .Value;

        // Assert
        Assert.Same(builder, result);
        Assert.Equal(TimeSpan.FromSeconds(7), options.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(3), options.InitialDelay);
        Assert.Equal(TimeSpan.FromMinutes(2), options.MaxDelay);
        Assert.Equal(5, options.MaxAttempts);
        Assert.Equal(12, options.BatchSize);
    }

    [Theory]
    [InlineData("poll-zero")]
    [InlineData("poll-too-large")]
    [InlineData("initial-zero")]
    [InlineData("max-below-initial")]
    [InlineData("max-too-large")]
    [InlineData("attempts-negative")]
    [InlineData("batch-zero")]
    public void UseCorePublisher_WhenRetryOptionsAreInvalid_RejectsOptions(
        string scenario)
    {
        // Arrange
        var services = new ServiceCollection();

        new ReportingModuleBuilder(services).UseCorePublisher(options =>
        {
            switch (scenario)
            {
                case "poll-zero":
                    options.PollInterval = TimeSpan.Zero;
                    break;

                case "poll-too-large":
                    options.PollInterval = TimeSpan.FromDays(2);
                    break;

                case "initial-zero":
                    options.InitialDelay = TimeSpan.Zero;
                    break;

                case "max-below-initial":
                    options.MaxDelay = options.InitialDelay / 2;
                    break;

                case "max-too-large":
                    options.MaxDelay = TimeSpan.FromDays(366);
                    break;

                case "attempts-negative":
                    options.MaxAttempts = -1;
                    break;

                case "batch-zero":
                    options.BatchSize = 0;
                    break;
            }
        });

        using var provider = services.BuildServiceProvider();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => provider
            .GetRequiredService<IOptions<ReportRetryOptions>>()
            .Value;

        // Assert
        Assert.Throws<OptionsValidationException>(action);
    }
}
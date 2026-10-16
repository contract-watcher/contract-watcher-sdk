using ContractWatcher.SDK.Configuration.Builders;
using ContractWatcher.SDK.Configuration.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Configuration.Extensions;

public sealed class ContractWatcherBuilderExtensionsTests
{
    [Fact]
    public void ModuleExtensions_WhenBuilderIsNull_ThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddContracts(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddCoreContracts(null!));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddValidation(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddDefaultValidation(null!));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddReporting(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddDefaultReporting(null!));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddExecution(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddDefaultExecution(null!));
        Assert.Throws<ArgumentNullException>(() => ContractWatcherBuilderExtensions.AddDefaults(null!));
    }

    [Fact]
    public void ModuleExtensions_WhenConfigureIsNull_ThrowArgumentNullException()
    {
        // Arrange
        var builder = new ContractWatcherBuilder(new ServiceCollection());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.AddContracts(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddValidation(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddReporting(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddExecution(null!));
    }
}
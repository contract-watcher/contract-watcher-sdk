using ContractWatcher.SDK.Configuration.Builders;
using ContractWatcher.SDK.Contracts.Builders;
using ContractWatcher.SDK.Reporting.Builders;

namespace ContractWatcher.SDK.Tests.Configuration.Builders;

public sealed class BuilderGuardTests
{
    [Fact]
    public void ContractWatcherBuilder_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action action = () =>  _ = new ContractWatcherBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void ContractModuleBuilder_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action action = () => _ = new ContractModuleBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void ReportingModuleBuilder_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Act
        Action action = () =>  _ = new ReportingModuleBuilder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
}
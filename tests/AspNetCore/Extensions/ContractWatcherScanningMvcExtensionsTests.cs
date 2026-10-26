using ContractWatcher.SDK.AspNetCore.Extensions;

namespace ContractWatcher.SDK.Tests.AspNetCore.Extensions;

public sealed class ContractWatcherScanningMvcExtensionsTests
{
    [Fact]
    public void AddContractWatcherScanning_WhenBuilderIsNull_Throws()
    {
        // Act
        var action = () =>
            ContractWatcherScanningMvcExtensions.AddContractWatcherScanning(null!);

        // Assert
        Assert.Throws<ArgumentNullException>("builder", action);
    }
}
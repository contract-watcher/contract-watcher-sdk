using ContractWatcher.SDK.Contracts.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ContractWatcher.SDK.Tests.Contracts.Extensions;

public sealed class ContractServiceCollectionExtensionsTests
{
    [Fact]
    public void AddContractWatcherContracts_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var action = () => services.AddContractWatcherContracts();

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
}
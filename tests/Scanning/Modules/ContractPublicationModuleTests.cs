using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Tests.Fixtures;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class ContractPublicationModuleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenPrerequisiteIsMissing_RejectsPublication(bool mapped)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        var module = new ContractPublicationModule(delivery.Delivery);

        var context = new ScanningContext { MappingResults = mapped ? [] : null };

        // Act
        var action = () => module.ExecuteAsync(context, default);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        await Assert.ThrowsAsync<ArgumentNullException>(() => module.ExecuteAsync(null!, default));

        fixture.Client.VerifyNoOtherCalls();
    }
}
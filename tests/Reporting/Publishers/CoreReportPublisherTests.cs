using ContractWatcher.SDK.Reporting.Publishers;
using ContractWatcher.SDK.Tests.Fixtures;

namespace ContractWatcher.SDK.Tests.Reporting.Publishers;

public sealed class CoreReportPublisherTests
{
    [Fact]
    public async Task CorePublisher_WithNullReport_ThrowsArgumentNullException()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();

        var publisher = new CoreReportPublisher(session.Delivery);

        // Act
        var action = () => publisher.PublishAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }
}
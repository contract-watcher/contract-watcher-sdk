using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Reporting.Publishers;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Reporting.Publishers;

public sealed class CoreReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithNullReport_ThrowsArgumentNullException()
    {
        // Arrange
        var coreClient = new Mock<IContractWatcherCoreClient>();
        var publisher = new CoreReportPublisher(coreClient.Object);

        // Act
        var act = () => publisher.PublishAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task PublishAsync_WithValidReport_PublishesReportToCore()
    {
        // Arrange
        var coreClient = new Mock<IContractWatcherCoreClient>();
        var publisher = new CoreReportPublisher(coreClient.Object);

        var report = CreateReport();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        coreClient
            .Setup(client => client.PublishReportAsync(report, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await publisher.PublishAsync(report, cancellationToken);

        // Assert
        coreClient.Verify(client => client.PublishReportAsync(report, cancellationToken), Times.Once);
    }
}
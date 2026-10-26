using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Refreshers;
using ContractWatcher.SDK.Core.Interfaces;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Contracts.Refreshers;

public sealed class ContractRefresherTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RefreshAsync_WhenSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? slug)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var coreClient = new Mock<IContractWatcherCoreClient>();
        var refresher = new ContractRefresher(store.Object, coreClient.Object);

        // Act
        var action = () => refresher.RefreshAsync(slug!);

        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task RefreshAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var coreClient = new Mock<IContractWatcherCoreClient>();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var token = cancellationTokenSource.Token;
        var refresher = new ContractRefresher(store.Object, coreClient.Object);

        // Act
        var action = () => refresher.RefreshAsync("products", token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);

        coreClient.Verify(x => 
            x.GetContractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenContractDoesNotExist_ReturnsNull()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var coreClient = new Mock<IContractWatcherCoreClient>();

        coreClient.Setup(x => 
                x.GetContractAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedContract?)null);

        var refresher = new ContractRefresher(store.Object, coreClient.Object);

        // Act
        var result = await refresher.RefreshAsync("products");

        // Assert
        Assert.Null(result);
        store.Verify(x => 
            x.SetAsync(It.IsAny<PublishedContract>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenContractExists_StoresAndReturnsContract()
    {
        // Arrange
        var contract = CreateContract();
        var store = new Mock<IContractStore>();
        var coreClient = new Mock<IContractWatcherCoreClient>();

        coreClient.Setup(x => x.GetContractAsync("products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);

        var refresher = new ContractRefresher(store.Object, coreClient.Object);

        // Act
        var result = await refresher.RefreshAsync("products");

        // Assert
        Assert.Same(contract, result);
        store.Verify(x => x.SetAsync(contract, It.IsAny<CancellationToken>()), Times.Once);
    }
}
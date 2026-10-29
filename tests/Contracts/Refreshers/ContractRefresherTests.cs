using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Refreshers;
using ContractWatcher.SDK.Core.Interfaces;
using Moq;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
// ReSharper disable AccessToDisposedClosure

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
    [Fact]
    public async Task RefreshManyAsync_WhenResultsAreMixed_SavesSuccessAndAbsenceInOneBatch()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var client = new Mock<IContractWatcherCoreClient>();
        var contract = CreateContract("found", version: 4);
        client.Setup(value => value.GetContractAsync("found", default)).ReturnsAsync(contract);
        client.Setup(value => value.GetContractAsync("offline", default))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.NetworkError, "Offline"));
        var refresher = new ContractRefresher(store.Object, client.Object);

        // Act
        var results = await refresher.RefreshManyAsync(["found", "missing", "offline", "found"]);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.NotNull(results.Single(result => result.Slug == "offline").Error);
        store.Verify(value => value.ApplyAsync(
            It.Is<IReadOnlyCollection<PublishedContract>>(items => items.Count == 1 && items.Single() == contract),
            It.Is<IReadOnlyCollection<string>>(items => items.Count == 1 && items.Single() == "missing"),
            default), Times.Once);
        store.VerifyNoOtherCalls();
        client.Verify(value => value.GetContractAsync("found", default), Times.Once);
    }

    [Fact]
    public async Task RefreshManyAsync_WhenPersistenceFails_DoesNotReportNetworkFailure()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var client = new Mock<IContractWatcherCoreClient>();
        store.Setup(value => value.ApplyAsync(
                It.IsAny<IReadOnlyCollection<PublishedContract>>(),
                It.IsAny<IReadOnlyCollection<string>>(), default))
            .ThrowsAsync(new IOException("Disk unavailable"));
        var refresher = new ContractRefresher(store.Object, client.Object);

        // Act
        var action = () => refresher.RefreshManyAsync(["products"]);

        // Assert
        await Assert.ThrowsAsync<IOException>(action);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshManyAsync_WhenSlugsAreInvalid_DoesNotCallDependencies(bool nullCollection)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var client = new Mock<IContractWatcherCoreClient>();
    
        var refresher = new ContractRefresher(store.Object, client.Object);
    
        // Act
        var action = () => refresher.RefreshManyAsync(nullCollection ? null! : new[] { " " });
    
        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(action);
    
        client.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }
    
    [Theory]
    [InlineData("other", 1)]
    [InlineData("products", 0)]
    public async Task RefreshManyAsync_WhenCoreReturnsInvalidContract_DoesNotSave(string slug, int version)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var client = new Mock<IContractWatcherCoreClient>();
    
        client
            .Setup(value => value.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(slug, version));
    
        var refresher = new ContractRefresher(store.Object, client.Object);
    
        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            refresher.RefreshManyAsync(["products"]));
    
        // Assert
        store.VerifyNoOtherCalls();
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshManyAsync_WhenCanceled_DoesNotApplyBatch(bool afterRequest)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
    
        var store = new Mock<IContractStore>();
        var client = new Mock<IContractWatcherCoreClient>();
    
        if (afterRequest)
        {
            client
                .Setup(value => value.GetContractAsync("products", cancellation.Token))
                .Callback(() => cancellation.Cancel())
                .ReturnsAsync(CreateContract());
        }
        else
        {
            cancellation.Cancel();
        }
    
        var refresher = new ContractRefresher(store.Object, client.Object);
    
        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            refresher.RefreshManyAsync(["products"], cancellation.Token));
    
        // Assert
        store.VerifyNoOtherCalls();
    
        client.Verify(value => 
            value.GetContractAsync("products", cancellation.Token), afterRequest ? Times.Once() : Times.Never());
    }
}
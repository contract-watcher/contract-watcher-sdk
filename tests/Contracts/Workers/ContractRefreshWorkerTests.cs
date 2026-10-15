using ContractWatcher.SDK.Configuration.Options;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Workers;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ContractWatcher.SDK.Tests.Contracts.Workers;

public sealed class ContractRefreshWorkerTests
{
    [Fact]
    public async Task RefreshKnownContractsAsync_WhenContractsExist_RefreshesContracts()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns(["products", "customers"]);

        var worker = CreateWorker(store.Object, refresher.Object);

        // Act
        await worker.RefreshKnownContractsAsync(CancellationToken.None);

        // Assert
        refresher.Verify(x => 
            x.RefreshAsync("products", CancellationToken.None), Times.Once);

        refresher.Verify(x => 
            x.RefreshAsync("customers", CancellationToken.None), Times.Once);
    }
    
    [Fact]
    public async Task RefreshKnownContractsAsync_WhenRefreshThrowsCoreRequestException_ContinuesProcessing()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns(["products", "customers"]);

        refresher
            .Setup(x => x.RefreshAsync(
                "products",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.ServerError, "Core unavailable."));

        var worker = CreateWorker(store.Object, refresher.Object);

        // Act
        await worker.RefreshKnownContractsAsync(CancellationToken.None);

        // Assert
        refresher.Verify(x => 
            x.RefreshAsync("customers", CancellationToken.None), Times.Once);
    }
    
    [Fact]
    public async Task RefreshKnownContractsAsync_WhenStoreIsEmpty_DoesNotRefreshContracts()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns([]);

        var worker = CreateWorker(store.Object, refresher.Object);

        // Act
        await worker.RefreshKnownContractsAsync(CancellationToken.None);

        // Assert
        refresher.Verify(x => x.RefreshAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task RefreshKnownContractsAsync_WhenCancellationRequested_PropagatesOperationCanceledException()
    {
        // Arrange
        var contractStore = new Mock<IContractStore>();
        var contractRefresher = new Mock<IContractRefresher>();
        
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        contractStore
            .Setup(x => x.GetSlugs())
            .Returns(["products"]);

        // ReSharper disable once AccessToDisposedClosure
        contractRefresher
            .Setup(x => x.RefreshAsync("products", cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationTokenSource.Token));

        var worker = new ContractRefreshWorker(
            contractStore.Object,
            contractRefresher.Object,
            Options.Create(new ContractWatcherOptions()),
            TimeProvider.System,
            NullLogger<ContractRefreshWorker>.Instance);

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var act = () => worker.RefreshKnownContractsAsync(cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }
    
    [Fact]
    public async Task RefreshKnownContractsAsync_WhenUnexpectedExceptionOccurs_DoesNotThrow()
    {
        // Arrange
        var contractStore = new Mock<IContractStore>();
        var contractRefresher = new Mock<IContractRefresher>();

        contractStore
            .Setup(x => x.GetSlugs())
            .Returns(["products"]);

        contractRefresher
            .Setup(x => x.RefreshAsync("products", CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Unexpected error."));

        var worker = new ContractRefreshWorker(
            contractStore.Object,
            contractRefresher.Object,
            Options.Create(new ContractWatcherOptions()),
            TimeProvider.System,
            NullLogger<ContractRefreshWorker>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => 
            worker.RefreshKnownContractsAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private ContractRefreshWorker CreateWorker(IContractStore contractStore, IContractRefresher contractRefresher)
    {
        var timeProvider = new FakeTimeProvider();
        
        var options = Options.Create(new ContractWatcherOptions
        {
            CoreUrl = "https://localhost/api/sdk",
            IntegrationKey = "test-key",
            ContractRefreshInterval = TimeSpan.FromMinutes(5)
        });
        
        return new ContractRefreshWorker(
            contractStore,
            contractRefresher,
            options,
            timeProvider,
            NullLogger<ContractRefreshWorker>.Instance);
    }
}
using ContractWatcher.Common.Contracts;
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
    public async Task ExecuteAsync_WhenContractsExist_RefreshesContracts()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns(["products", "customers"]);

        var refreshed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedSlugs = new HashSet<string>();

        refresher
            .Setup(x => x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>(
                (slug, _) =>
                {
                    refreshedSlugs.Add(slug);

                    if (refreshedSlugs.Count == 2)
                        refreshed.TrySetResult(true);
                })
            .ReturnsAsync((PublishedContract?)null);

        var (worker, timeProvider) = CreateWorker(store.Object, refresher.Object);
        
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(10);

        // Act
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains("products", refreshedSlugs);
        Assert.Contains("customers", refreshedSlugs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshThrowsCoreRequestException_ContinuesProcessing()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns(["products", "customers"]);

        var refreshed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        refresher
            .Setup(x => x.RefreshAsync("products", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.ServerError, "Core unavailable."));

        refresher
            .Setup(x => x.RefreshAsync("customers", It.IsAny<CancellationToken>()))
            .Callback(() => refreshed.TrySetResult(true))
            .ReturnsAsync((PublishedContract?)null);

        var (worker, timeProvider) = CreateWorker(store.Object, refresher.Object);
        
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(10);

        // Act
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        refresher.Verify(x => 
            x.RefreshAsync("products", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        refresher.Verify(x => 
            x.RefreshAsync("customers", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoreIsEmpty_DoesNotRefreshContracts()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();

        store
            .Setup(x => x.GetSlugs())
            .Returns([]);

        var storeRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        store
            .Setup(x => x.GetSlugs())
            .Callback(() => storeRead.TrySetResult(true))
            .Returns([]);

        var (worker, timeProvider) = CreateWorker(store.Object, refresher.Object);
        
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(10);

        // Act
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        
        await storeRead.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        // Assert
        refresher.Verify(x => 
            x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_BeforeIntervalElapsed_DoesNotRefreshContract()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
        var interval = TimeSpan.FromMinutes(5);

        store
            .Setup(x => x.GetSlugs())
            .Returns(["products"]);

        var (worker, timeProvider) = CreateWorker(store.Object, refresher.Object);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(10);

        // Act
        timeProvider.Advance(interval - TimeSpan.FromSeconds(1));

        // Assert
        refresher.Verify(x => 
            x.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await worker.StopAsync(CancellationToken.None);
    }

    private static (ContractRefreshWorker Worker, FakeTimeProvider TimeProvider) 
        CreateWorker(IContractStore contractStore, IContractRefresher contractRefresher)
    {
        var timeProvider = new FakeTimeProvider();
        
        var options = Options.Create(new ContractWatcherOptions
        {
            CoreUrl = "https://localhost/api/sdk",
            IntegrationKey = "test-key",
            ContractRefreshInterval = TimeSpan.FromMinutes(5)
        });
        
        var worker = new ContractRefreshWorker(
            contractStore,
            contractRefresher,
            options,
            timeProvider,
            NullLogger<ContractRefreshWorker>.Instance);

        return (worker, timeProvider);
    }
}
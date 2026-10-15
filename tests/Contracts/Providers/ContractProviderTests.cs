using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Providers;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Contracts.Providers;

public sealed class ContractProviderTests
{
    [Fact]
    public void Constructor_WhenContractStoreIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var refresher = new Mock<IContractRefresher>();
        
        // Act
        var action = () => new ContractProvider(null!, refresher.Object);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
    
    [Fact]
    public void Constructor_WhenRefresherIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new Mock<IContractStore>();

        // Act
        var action = () => new ContractProvider(store.Object, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public async Task GetAsync_WhenContractExists_ReturnsContract()
    {
        // Arrange
        var contract = CreateContract();
        var store = new Mock<IContractStore>();
        store.Setup(x => x.Get("products")).Returns(contract);
        var refresher = new Mock<IContractRefresher>();
        var provider = new ContractProvider(store.Object, refresher.Object);

        // Act
        var result = await provider.GetAsync("products");

        // Assert
        Assert.Same(contract, result);
    }

    [Fact]
    public async Task GetAsync_WhenContractDoesNotExist_ReturnsNull()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        store.Setup(x => x.Get("products")).Returns((PublishedContract?)null);
        var refresher = new Mock<IContractRefresher>();
        var provider = new ContractProvider(store.Object, refresher.Object);

        // Act
        var result = await provider.GetAsync("products");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetAsync_WhenSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? slug)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
        var provider = new ContractProvider(store.Object, refresher.Object);

        // Act
        var action = () => provider.GetAsync(slug!);

        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task GetAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
        var provider = new ContractProvider(store.Object, refresher.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => provider.GetAsync("products", cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }
}
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Providers;
using Moq;

namespace ContractWatcher.SDK.Tests.Contracts.Providers;

public sealed class ContractProviderTests
{
    [Fact]
    public void Constructor_WhenContractStoreIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ContractProvider(null!);

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
        var provider = new ContractProvider(store.Object);

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
        var provider = new ContractProvider(store.Object);

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
        var provider = new ContractProvider(store.Object);

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
        var provider = new ContractProvider(store.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var action = () => provider.GetAsync("products", cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    private static PublishedContract CreateContract() =>
        new()
        {
            Slug = "products",
            ContractVersion = 1,
            Rules = []
        };
}
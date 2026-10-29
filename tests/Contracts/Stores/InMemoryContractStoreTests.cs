using ContractWatcher.SDK.Contracts.Stores;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Contracts.Stores;

public sealed class InMemoryContractStoreTests
{
    [Fact]
    public void Set_WhenContractDoesNotExist_StoresContract()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var contract = CreateContract(slug: "products", version: 1);

        // Act
        store.Set(contract);

        // Assert
        var result = store.Get("products");
        Assert.Same(contract, result);
    }

    [Fact]
    public void Get_WhenContractExists_ReturnsContract()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var contract = CreateContract(slug: "products", version: 1);
        store.Set(contract);

        // Act
        var result = store.Get("products");

        // Assert
        Assert.NotNull(result);
        Assert.Same(contract, result);
    }

    [Fact]
    public void Get_WhenContractDoesNotExist_ReturnsNull()
    {
        // Arrange
        var store = new InMemoryContractStore();

        // Act
        var result = store.Get("products");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_WhenContractsHaveDifferentSlugs_StoresBothContracts()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var products = CreateContract(slug: "products", version: 1);
        var customers = CreateContract(slug: "customers", version: 1);

        // Act
        store.Set(products);
        store.Set(customers);

        // Assert
        Assert.Same(products, store.Get("products"));
        Assert.Same(customers, store.Get("customers"));
    }

    [Fact]
    public void Set_WhenContractWithSameSlugExists_ReplacesContract()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var currentContract = CreateContract(slug: "products", version: 1);
        var newContract = CreateContract(slug: "products", version: 2);
        store.Set(currentContract);

        // Act
        store.Set(newContract);

        // Assert
        var result = store.Get("products");

        Assert.Same(newContract, result);
        Assert.Equal(2, result!.ContractVersion);
    }

    [Fact]
    public void Remove_WhenContractExists_RemovesContract()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var contract = CreateContract(slug: "products", version: 1);
        store.Set(contract);

        // Act
        var removed = store.Remove("products");

        // Assert
        Assert.True(removed);
        Assert.Null(store.Get("products"));
    }

    [Fact]
    public void Remove_WhenContractDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var store = new InMemoryContractStore();

        // Act
        var removed = store.Remove("products");

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void Clear_WhenContractsExist_RemovesAllContracts()
    {
        // Arrange
        var store = new InMemoryContractStore();
        store.Set(CreateContract(slug: "products", version: 1));
        store.Set(CreateContract(slug: "customers", version: 1));

        // Act
        store.Clear();

        // Assert
        Assert.Null(store.Get("products"));
        Assert.Null(store.Get("customers"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Get_WhenSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? slug)
    {
        // Arrange
        var store = new InMemoryContractStore();

        // Act
        var action = () => store.Get(slug!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Remove_WhenSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? slug)
    {
        // Arrange
        var store = new InMemoryContractStore();

        // Assert
        Assert.ThrowsAny<ArgumentException>(() => store.Remove(slug!));
    }

    [Fact]
    public void Set_WhenContractIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new InMemoryContractStore();

        // Act
        var action = () => store.Set(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Set_WhenContractSlugIsEmptyOrWhiteSpace_ThrowsArgumentException(string slug)
    {
        // Arrange
        var store = new InMemoryContractStore();
        var contract = CreateContract(slug: slug, version: 1);

        // Act
        var action = () => store.Set(contract);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }
    
    [Fact]
    public void GetSlugs_ReturnsStoredContractSlugs()
    {
        // Arrange
        var store = new InMemoryContractStore();
        store.Set(CreateContract(slug: "products"));
        store.Set(CreateContract(slug: "customers"));

        // Act
        var result = store.GetSlugs();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("products", result);
        Assert.Contains("customers", result);
    }
    
    [Fact]
    public async Task AsyncMethods_WhenNotCanceled_StoreRemoveAndClearContracts()
    {
        // Arrange
        var store = new InMemoryContractStore();
        var products = CreateContract(slug: "products");
        var customers = CreateContract(slug: "customers");

        // Act
        await store.SetAsync(products);
        await store.SetAsync(customers);

        // Assert
        Assert.Same(products, store.Get("products"));
        Assert.Same(customers, store.Get("customers"));
        Assert.True(await store.RemoveAsync("products"));
        Assert.False(await store.RemoveAsync("products"));
        Assert.Null(store.Get("products"));

        await store.ClearAsync();

        Assert.Empty(store.GetSlugs());
    }

    [Theory]
    [InlineData("set")]
    [InlineData("remove")]
    [InlineData("clear")]
    public async Task AsyncMethods_WhenCanceled_DoNotChangeStore(string operation)
    {
        // Arrange
        var store = new InMemoryContractStore();
        var original = CreateContract(version: 1);
        store.Set(original);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        Func<Task> action = operation switch
        {
            // ReSharper disable once AccessToDisposedClosure
            "set" => () => store.SetAsync(CreateContract(version: 2), cancellation.Token),
            // ReSharper disable once AccessToDisposedClosure
            "remove" => () => store.RemoveAsync(original.Slug, cancellation.Token),
            // ReSharper disable once AccessToDisposedClosure
            _ => () => store.ClearAsync(cancellation.Token)
        };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        Assert.Same(original, store.Get(original.Slug));
        Assert.Single(store.GetSlugs());
    }
}
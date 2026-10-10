using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Contracts.Stores;

namespace ContractWatcher.SDK.Tests.Stores;

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

    private static PublishedContract CreateContract(string slug, int version) =>
        new()
        {
            Slug = slug,
            ContractVersion = version,
            Rules =
            [
                new ContractRule
                {
                    FieldName = "id",
                    Type = ContractFieldType.String,
                    Required = true,
                    Nullable = false
                }
            ]
        };
}
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Tests.Fixtures;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Contracts.Stores;

public sealed class MicroDbContractStoreTests : IDisposable
{
    private readonly MicroDbFixture _fixture = new();

    private MicroDbSession Open() => _fixture.Open();

    [Fact]
    public async Task SetAsync_WhenContractExists_UpdatesTrackedInstanceAndPersists()
    {
        // Arrange
        using (var session = Open())
        {
            await session.Contracts.SetAsync(CreateContract(version: 1));

            var original = session.Context.Contracts.Find("products");

            // Act
            await session.Contracts.SetAsync(CreateContract(version: 2));

            // Assert
            Assert.Same(original, session.Context.Contracts.Find("products"));
            Assert.Equal(2, session.Contracts.Get("products")!.ContractVersion);
        }

        using var reopened = Open();

        Assert.Equal(2, reopened.Contracts.Get("products")!.ContractVersion);
    }

    [Fact]
    public async Task RemoveAndClearAsync_AfterRestart_DoNotRestoreDeletedContracts()
    {
        // Arrange
        using (var session = Open())
        {
            await session.Contracts.SetAsync(CreateContract("first"));
            await session.Contracts.SetAsync(CreateContract("second"));

            // Act
            var removed = await session.Contracts.RemoveAsync("first");

            // Assert
            Assert.True(removed);
        }

        using (var reopened = Open())
        {
            Assert.Null(reopened.Contracts.Get("first"));
            Assert.NotNull(reopened.Contracts.Get("second"));

            await reopened.Contracts.ClearAsync();
        }

        using var confirmed = Open();

        Assert.Empty(confirmed.Contracts.GetSlugs());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Store_WhenSlugIsInvalid_RejectsReadWriteAndRemove(string? slug)
    {
        // Arrange
        using var session = Open();
        var contract = CreateContract(slug!);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => session.Contracts.Get(slug!));
        await Assert.ThrowsAsync<ArgumentException>(() => session.Contracts.SetAsync(contract));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => session.Contracts.RemoveAsync(slug!));
    }

    [Fact]
    public async Task SetAsync_WhenContractIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = Open();

        // Act
        // ReSharper disable once AccessToDisposedClosure
        var action = () => session.Contracts.SetAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task Get_WhenCallerChangesReturnedCollection_DoesNotChangeStoredContract()
    {
        // Arrange
        using var session = Open();

        await session.Contracts.SetAsync(CreateContract());

        var dto = session.Contracts.Get("products")!;
        var rules = Assert.IsType<ContractRule[]>(dto.Rules);

        // Act
        rules[0] = new ContractRule
        {
            FieldName = "changed",
            Type = ContractFieldType.String,
            Required = false,
            Nullable = true
        };

        // Assert
        Assert.Equal("id", session.Contracts.Get("products")!.Rules.Single().FieldName);
        Assert.Equal(new[] { "products" }, session.Contracts.GetSlugs());
    }
    
    [Theory]
    [InlineData("contracts")]
    [InlineData("removed")]
    [InlineData("contract-slug")]
    [InlineData("removed-slug")]
    [InlineData("duplicate")]
    public async Task ApplyAsync_WhenBatchIsInvalid_DoesNotChangeStore(string invalid)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
    
        IReadOnlyCollection<PublishedContract> contracts = invalid switch
        {
            "contracts" => null!,
            "contract-slug" => [CreateContract(" ")],
            "duplicate" => [CreateContract(), CreateContract()],
            _ => []
        };
    
        IReadOnlyCollection<string> removed = invalid switch
        {
            "removed" => null!,
            "removed-slug" => [" "],
            _ => []
        };
    
        // Act
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            session.Contracts.ApplyAsync(contracts, removed));
    
        // Assert
        Assert.Empty(session.Contracts.GetSlugs());
    }
    
    [Fact]
    public async Task ApplyAsync_WhenCanceled_DoesNotChangeStore()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        using var cancellation = new CancellationTokenSource();
    
        cancellation.Cancel();
    
        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.Contracts.ApplyAsync(
                [CreateContract()],
                [],
                cancellation.Token));
    
        // Assert
        Assert.Empty(session.Contracts.GetSlugs());
    }
    
    [Fact]
    public async Task ApplyAsync_WhenContractExists_UpdatesTrackedInstanceAndPersistsBatch()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
    
        using (var session = fixture.Open())
        {
            await session.Contracts.SetAsync(CreateContract());
            await session.Contracts.SetAsync(CreateContract("removed"));
    
            var original = session.Context.Contracts.Find("products");
    
            // Act
            await session.Contracts.ApplyAsync(
                [CreateContract(version: 9, rules: [])], ["removed"]);
    
            // Assert
            Assert.Same(original, session.Context.Contracts.Find("products"));
        }
    
        using var reopened = fixture.Open();
    
        var restored = reopened.Contracts.Get("products")!;
    
        Assert.Equal(9, restored.ContractVersion);
        Assert.Empty(restored.Rules);
        Assert.Null(reopened.Contracts.Get("removed"));
    }

    public void Dispose() => _fixture.Dispose();
}
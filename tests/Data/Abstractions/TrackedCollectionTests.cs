using ContractWatcher.SDK.Data.Abstractions;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Mappings;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Data.Abstractions;

public sealed class TrackedCollectionTests
{
    [Fact]
    public void Add_WhenItemIsNull_ThrowsWithoutChangingCollection()
    {
        // Arrange
        var collection = new TrackedCollection<ContractRuleEntity>();

        // Act
        var action = () => collection.Add(null!);

        // Assert
        Assert.Throws<ArgumentNullException>("item", action);
        Assert.Empty(collection);
    }

    [Fact]
    public void SetItem_WhenItemIsNull_KeepsOriginalItem()
    {
        // Arrange
        var item = Assert.Single(CreateContract().ToEntity(default).Rules);
        var collection = new TrackedCollection<ContractRuleEntity> { item };

        // Act
        Action action = () => collection[0] = null!;

        // Assert
        Assert.Throws<ArgumentNullException>("item", action);
        Assert.Same(item, Assert.Single(collection));
    }

    [Fact]
    public void Changes_WhenNoSubscribersExist_UpdateCollectionAndItem()
    {
        // Arrange
        var item = Assert.Single(CreateContract().ToEntity(default).Rules);
        var collection = new TrackedCollection<ContractRuleEntity>();

        // Act
        collection.Add(item);
        item.FieldName = "changed";

        // Assert
        Assert.Same(item, Assert.Single(collection));
        Assert.Equal("changed", collection[0].FieldName);
    }
}
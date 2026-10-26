using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Mappings;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Data.Mappings;

public sealed class ContractMappingTests
{
    [Fact]
    public void ToEntity_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        PublishedContract source = null!;

        // Act
        var action = () => source.ToEntity(default);

        // Assert
        Assert.Throws<ArgumentNullException>("source", action);
    }

    [Fact]
    public void ToDto_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ContractEntity source = null!;

        // Act
        var action = () => source.ToDto();

        // Assert
        Assert.Throws<ArgumentNullException>("source", action);
    }

    [Fact]
    public void Apply_WhenTargetIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ContractEntity target = null!;
        var source = CreateContract();

        // Act
        var action = () => target.Apply(source, default);

        // Assert
        Assert.Throws<ArgumentNullException>("target", action);
    }

    [Fact]
    public void Apply_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var target = CreateContract().ToEntity(default);

        // Act
        var action = () => target.Apply(null!, default);

        // Assert
        Assert.Throws<ArgumentNullException>("source", action);
    }

    [Fact]
    public void Apply_WhenSlugChanges_ThrowsWithoutChangingEntity()
    {
        // Arrange
        var updatedAt = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
        var target = CreateContract(slug: "products", version: 1).ToEntity(updatedAt);

        var rule = Assert.Single(target.Rules);
        var source = CreateContract(slug: "customers", version: 2);

        // Act
        var action = () => target.Apply(source, updatedAt.AddMinutes(1));

        // Assert
        Assert.Throws<ArgumentException>("source", action);

        Assert.Equal("products", target.Slug);
        Assert.Equal(1, target.ContractVersion);
        Assert.Equal(updatedAt, target.UpdatedAt);
        Assert.Same(rule, Assert.Single(target.Rules));
    }
}
using ContractWatcher.SDK.AspNetCore.Attributes;

namespace ContractWatcher.SDK.Tests.AspNetCore.Attributes;

public sealed class ContractWatcherAttributeTests
{
    [Fact]
    public void Constructor_WhenContractSlugIsValid_SetsContractSlug()
    {
        // Arrange
        const string contractSlug = "products";

        // Act
        var attribute = new ContractWatcherAttribute(contractSlug);

        // Assert
        Assert.Equal(contractSlug, attribute.ContractSlug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenContractSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? contractSlug)
    {
        // Act
        var action = () => new ContractWatcherAttribute(contractSlug!);

        // Assert
        Assert.ThrowsAny<ArgumentException>(action);
    }
}
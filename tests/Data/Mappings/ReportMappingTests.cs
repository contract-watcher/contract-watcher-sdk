using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Mappings;

namespace ContractWatcher.SDK.Tests.Data.Mappings;

public sealed class ReportMappingTests
{
    [Fact]
    public void ToEntity_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ValidationReport source = null!;

        // Act
        var action = () => source.ToEntity(default);

        // Assert
        Assert.Throws<ArgumentNullException>("source", action);
    }

    [Fact]
    public void ToDto_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ReportEntity source = null!;

        // Act
        var action = () => source.ToDto();

        // Assert
        Assert.Throws<ArgumentNullException>("source", action);
    }
}
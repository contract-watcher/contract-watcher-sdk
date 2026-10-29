using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Contracts.Comparers;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Contracts.Comparers;

public sealed class ContractContentComparerTests
{
    [Fact]
    public void AreEqual_WhenVersionAndOrderDiffer_ReturnsTrue()
    {
        // Arrange
        ContractRule[] rules =
        [
            new()
            {
                FieldName = "id",
                Type = ContractFieldType.String,
                Required = true,
                Nullable = false
            },
            new()
            {
                FieldName = "count",
                Type = ContractFieldType.Number,
                Required = false,
                Nullable = true
            }
        ];

        var left = CreateContract(version: 1, rules: rules);
        var right = CreateContract(version: 9, rules: rules.Reverse().ToArray());

        // Act
        var equal = ContractContentComparer.AreEqual(left, right);

        // Assert
        Assert.True(equal);
    }
}
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Tests.Fixtures;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class ContractPersistenceModuleTests
{
    [Fact]
    public async Task ExecuteAsync_WhenContextIsNull_Throws()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();

        var module = new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock);

        // Act
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            module.ExecuteAsync(null!, default));

        // Assert
        Assert.Empty(session.Context.ScannedContracts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceled_DoesNotChangeDatabase()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        var module = new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            module.ExecuteAsync(
                new ScanningContext { MappingResults = [] },
                cancellation.Token));

        // Assert
        Assert.Empty(session.Context.ScannedContracts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugIsInvalid_DoesNotPersistPartialBatch()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();

        var context = new ScanningContext
        {
            MappingResults =
            [
                new ContractMappingResult
                {
                    Slug = "valid",
                    Rules = []
                },
                new ContractMappingResult
                {
                    Slug = " ",
                    Rules = []
                }
            ]
        };

        var module = new ContractPersistenceModule(
            session.Contracts,
            session.Access,
            fixture.Clock);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            module.ExecuteAsync(context, default));

        // Assert
        Assert.Empty(session.Context.ScannedContracts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntryExists_UpdatesTrackedInstanceAndSurvivesRestart()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        using (var session = fixture.Open())
        {
            var module = new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock);

            await module.ExecuteAsync(new ScanningContext
            {
                MappingResults =
                [
                    new ContractMappingResult
                    {
                        Slug = "products",
                        Rules = [],
                        ExpectedVersion = 7,
                        TargetVersion = 8,
                        Pending = true
                    }
                ]
            }, default);

            var original = session.Context.ScannedContracts.Find("products");

            fixture.Clock.Advance(TimeSpan.FromMinutes(1));

            var context = new ScanningContext
            {
                MappingResults =
                [
                    new ContractMappingResult
                    {
                        Slug = "products",
                        ExpectedVersion = 8,
                        TargetVersion = null,
                        Pending = false,
                        Rules =
                        [
                            new ContractRule
                            {
                                FieldName = "name",
                                Type = ContractFieldType.String,
                                Required = true,
                                Nullable = false
                            }
                        ]
                    }
                ]
            };

            // Act
            await module.ExecuteAsync(context, default);

            // Assert
            Assert.Same(original, session.Context.ScannedContracts.Find("products"));
        }

        using var reopened = fixture.Open();

        var restored = reopened.Context.ScannedContracts.Find("products")!;

        Assert.Equal(8, restored.ExpectedVersion);
        Assert.Null(restored.TargetVersion);
        Assert.False(restored.Pending);
        Assert.Equal(fixture.Clock.GetUtcNow(), restored.ScannedAt);

        var rule = Assert.Single(restored.Rules);

        Assert.Equal("name", rule.FieldName);
        Assert.Equal(ContractFieldType.String, rule.Type);
        Assert.True(rule.Required);
        Assert.False(rule.Nullable);
    }
}
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.MicroDb.Options;
using ContractWatcher.MicroDb.Tracking.Enums;
using ContractWatcher.SDK.Data;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Enums;
using ContractWatcher.SDK.Data.Mappings;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Data;

public sealed class ContractWatcherDbContextTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private ContractWatcherDbContext Open() => new(new MicroDbOptions { DirectoryPath = _directory });

    [Fact]
    public async Task SaveChangesAsync_WhenNestedRuleChanges_PersistsChangeWithoutUpdate()
    {
        // Arrange
        using (var database = Open())
        {
            database.Contracts.Add(CreateContract().ToEntity(Now));
            await database.SaveChangesAsync();

            var entity = database.Contracts.Find("products")!;

            // Act
            entity.Rules[0].Type = ContractFieldType.Number;

            // Assert
            Assert.Equal(EntityState.Modified, database.Contracts.GetState("products"));

            await database.SaveChangesAsync();

            Assert.Equal(EntityState.Unchanged, database.Contracts.GetState("products"));
        }

        using var reopened = Open();

        Assert.Equal(ContractFieldType.Number, reopened.Contracts.Find("products")!.Rules[0].Type);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenLoadedNestedRuleChanges_RestoresEventSubscriptions()
    {
        // Arrange
        using (var database = Open())
        {
            database.Contracts.Add(CreateContract().ToEntity(Now));
            await database.SaveChangesAsync();
        }

        using (var reopened = Open())
        {
            var entity = reopened.Contracts.Find("products")!;

            // Act
            entity.Rules[0].Nullable = true;

            // Assert
            Assert.Equal(EntityState.Modified, reopened.Contracts.GetState("products"));

            await reopened.SaveChangesAsync();
        }

        using var confirmed = Open();

        Assert.True(confirmed.Contracts.Find("products")!.Rules[0].Nullable);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("clear")]
    [InlineData("move")]
    public async Task SaveChangesAsync_WhenCollectionChanges_PersistsStructure(string operation)
    {
        // Arrange
        ContractRuleEntity Rule(string name) => new()
        {
            FieldName = name,
            Type = ContractFieldType.String,
            Required = true,
            Nullable = false
        };

        string[] expected;

        using (var database = Open())
        {
            var entity = CreateContract().ToEntity(Now);
            entity.Rules.Add(Rule("name"));

            database.Contracts.Add(entity);
            await database.SaveChangesAsync();

            // Act
            switch (operation)
            {
                case "add":
                    entity.Rules.Add(Rule("email"));
                    break;

                case "remove":
                    entity.Rules.RemoveAt(0);
                    break;

                case "replace":
                    entity.Rules[0] = Rule("email");
                    break;

                case "clear":
                    entity.Rules.Clear();
                    break;

                case "move":
                    entity.Rules.Move(0, 1);
                    break;
            }

            expected = entity.Rules
                .Select(rule => rule.FieldName)
                .ToArray();

            // Assert
            Assert.Equal(EntityState.Modified, database.Contracts.GetState(entity.Slug));

            await database.SaveChangesAsync();
        }

        using var reopened = Open();

        Assert.Equal(expected, reopened.Contracts.Find("products")!.Rules
            .Select(rule => rule.FieldName));
    }

    [Fact]
    public async Task GetState_WhenNestedValueIsReverted_ReturnsUnchanged()
    {
        // Arrange
        using var database = Open();

        var entity = CreateContract().ToEntity(Now);

        database.Contracts.Add(entity);
        await database.SaveChangesAsync();

        // Act
        entity.Rules[0].Required = false;
        entity.Rules[0].Required = true;

        // Assert
        Assert.Equal(EntityState.Unchanged, database.Contracts.GetState(entity.Slug));
        Assert.Equal(0, await database.SaveChangesAsync());
    }

    [Fact]
    public void Changed_WhenRemovedChildChanges_DoesNotNotifyParent()
    {
        // Arrange
        var entity = CreateContract().ToEntity(Now);
        var oldRule = entity.Rules[0];

        entity.Rules.Clear();

        var changes = 0;
        entity.Changed += (_, _) => changes++;

        // Act
        oldRule.Required = false;

        // Assert
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Changed_WhenSameChildAppearsTwice_KeepsSubscriptionUntilLastRemoval()
    {
        // Arrange
        var entity = CreateContract().ToEntity(Now);
        var rule = entity.Rules[0];

        entity.Rules.Add(rule);
        entity.Rules.RemoveAt(0);

        var changes = 0;
        entity.Changed += (_, _) => changes++;

        // Act
        rule.Required = false;

        // Assert
        Assert.Equal(1, changes);

        entity.Rules.Clear();
        changes = 0;

        rule.Required = true;

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenReportAndRetryMutate_PersistsTrackedProperties()
    {
        // Arrange
        var dto = CreateReport(violations:
        [
            new ValidationViolation
            {
                FieldName = "name",
                Type = ViolationType.TypeMismatch,
                ExpectedType = ContractFieldType.String,
                ActualType = ContractFieldType.Number
            }
        ]);

        using (var database = Open())
        {
            database.Reports.Add(dto.ToEntity(Now));

            database.ReportRetries.Add(new ReportRetryEntity
            {
                ReportId = dto.ReportId
            });

            await database.SaveChangesAsync();
        }

        using (var database = Open())
        {
            var report = database.Reports.Find(dto.ReportId)!;
            var retry = database.ReportRetries.Find(dto.ReportId)!;

            // Act
            report.Violations[0].FieldName = "email";
            report.DeliveryState = ReportDeliveryState.Failed;

            retry.FailedAttempts++;
            retry.LastAttemptAt = Now;
            retry.NextAttemptAt = Now.AddMinutes(1);
            retry.LastError = "Core unavailable";

            // Assert
            Assert.Equal(EntityState.Modified, database.Reports.GetState(dto.ReportId));
            Assert.Equal(EntityState.Modified, database.ReportRetries.GetState(dto.ReportId));

            await database.SaveChangesAsync();
        }

        using var reopened = Open();

        Assert.Equal("email", reopened.Reports.Find(dto.ReportId)!.Violations[0].FieldName);
        Assert.Equal(ReportDeliveryState.Failed, reopened.Reports.Find(dto.ReportId)!.DeliveryState);
        Assert.Equal(1, reopened.ReportRetries.Find(dto.ReportId)!.FailedAttempts);
        Assert.Equal(Now.AddMinutes(1), reopened.ReportRetries.Find(dto.ReportId)!.NextAttemptAt);
    }

    [Fact]
    public async Task Apply_WhenUpdatingExistingContract_PreservesTrackedInstance()
    {
        // Arrange
        using var database = Open();

        var entity = CreateContract(version: 1).ToEntity(Now);

        database.Contracts.Add(entity);
        await database.SaveChangesAsync();

        // Act
        entity.Apply(CreateContract(version: 2), Now.AddMinutes(1));

        // Assert
        Assert.Same(entity, database.Contracts.Find(entity.Slug));
        Assert.Equal(EntityState.Modified, database.Contracts.GetState(entity.Slug));

        await database.SaveChangesAsync();

        using var reopened = Open();

        Assert.Equal(2, reopened.Contracts.Find(entity.Slug)!.ContractVersion);
    }

    [Fact]
    public void ToDto_WhenEntityChanges_DoesNotMutatePreviouslyMappedDto()
    {
        // Arrange
        var original = CreateContract();
        var entity = original.ToEntity(Now);
        var snapshot = entity.ToDto();

        // Act
        entity.Rules[0].FieldName = "email";
        entity.ContractVersion++;

        // Assert
        Assert.Equal("id", snapshot.Rules.Single().FieldName);
        Assert.Equal("id", original.Rules.Single().FieldName);
        Assert.Equal(1, snapshot.ContractVersion);
    }

    [Fact]
    public void ToDto_ForReport_RoundTripsEveryWireField()
    {
        // Arrange
        var original = CreateReport(
            contractVersion: 12,
            status: ValidationStatus.Failed,
            violations:
            [
                new ValidationViolation
                {
                    FieldName = "id",
                    Type = ViolationType.TypeMismatch,
                    ExpectedType = ContractFieldType.String,
                    ActualType = ContractFieldType.Boolean
                }
            ]);

        original.Timestamp = Now;

        // Act
        var result = original
            .ToEntity(Now.AddMinutes(1))
            .ToDto();

        // Assert
        Assert.Equal(original.ReportId, result.ReportId);
        Assert.Equal(original.Slug, result.Slug);
        Assert.Equal(original.ContractVersion, result.ContractVersion);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Status, result.Status);

        var violation = Assert.Single(result.Violations);

        Assert.Equal("id", violation.FieldName);
        Assert.Equal(ViolationType.TypeMismatch, violation.Type);
        Assert.Equal(ContractFieldType.String, violation.ExpectedType);
        Assert.Equal(ContractFieldType.Boolean, violation.ActualType);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
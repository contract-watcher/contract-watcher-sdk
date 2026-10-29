using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Refreshers;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Scanning.Options;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;
// ReSharper disable AccessToDisposedClosure

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class ContractSynchronizationModuleTests
{
    [Theory]
    [InlineData(ContractSourcePreference.Application, true, 8)]
    [InlineData(ContractSourcePreference.Published, false, 7)]
    public async Task Pipeline_WhenCoreDiffers_SelectsVersionAndPersistsIntent(
        ContractSourcePreference preference, bool pending, int version)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        fixture.Client.Setup(client => client.GetContractAsync("products", default)).ReturnsAsync(CreateContract(version: 7));
        using (var session = fixture.Open())
        {
            var context = Input();

            // Act
            await Synchronize(session, fixture, context, preference);
            await new ContractMappingModule().ExecuteAsync(context, default);
            await new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock).ExecuteAsync(context, default);

            // Assert
            Assert.Equal(version, Assert.Single(context.Contracts!).ContractVersion);
            Assert.Equal(pending, Assert.Single(context.MappingResults!).Pending);
        }

        using var reopened = fixture.Open();
        Assert.Equal(7, reopened.Contracts.Get("products")!.ContractVersion);
        var intent = reopened.Context.ScannedContracts.Find("products")!;
        Assert.Equal(7, intent.ExpectedVersion);
        Assert.Equal(pending ? 8 : null, intent.TargetVersion);
        Assert.Equal(pending, intent.Pending);
        Assert.Empty(intent.Rules);
    }

    [Theory]
    [InlineData(ContractSourcePreference.Application, true)]
    [InlineData(ContractSourcePreference.Published, false)]
    public async Task Pipeline_WhenCoreUnavailable_PreservesCacheAndDoesNotInventVersion(
        ContractSourcePreference preference, bool pending)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        fixture.Client.Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.NetworkError, "Offline"));
        using (var session = fixture.Open())
        {
            await session.Contracts.SetAsync(CreateContract(version: 5));
            var context = Input();

            // Act
            await Synchronize(session, fixture, context, preference);
            await new ContractMappingModule().ExecuteAsync(context, default);
            await new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock).ExecuteAsync(context, default);

            // Assert
            Assert.Equal(5, Assert.Single(context.Contracts!).ContractVersion);
        }

        using var reopened = fixture.Open();
        var intent = reopened.Context.ScannedContracts.Find("products")!;
        Assert.Null(intent.ExpectedVersion);
        Assert.Null(intent.TargetVersion);
        Assert.Equal(pending, intent.Pending);
        Assert.Equal(5, reopened.Contracts.Get("products")!.ContractVersion);
    }

    [Fact]
    public async Task Mapping_WhenRepeated_AssignsSameVersion()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        fixture.Client.Setup(client => client.GetContractAsync("products", default)).ReturnsAsync(CreateContract(version: 7));
        var context = Input();
        await Synchronize(session, fixture, context);
        var mapper = new ContractMappingModule();

        // Act
        await mapper.ExecuteAsync(context, default);
        await mapper.ExecuteAsync(context, default);

        // Assert
        Assert.Equal(8, Assert.Single(context.Contracts!).ContractVersion);
        Assert.Equal(7, Assert.Single(context.MappingResults!).ExpectedVersion);
        fixture.Client.Verify(client => client.GetContractAsync("products", default), Times.Once);
    }

    [Fact]
    public async Task Mapping_WhenContractMissing_AssignsVersionOne()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        var context = Input();
        await Synchronize(session, fixture, context);

        // Act
        await new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        var result = Assert.Single(context.MappingResults!);
        Assert.Equal(0, result.ExpectedVersion);
        Assert.Equal(1, result.TargetVersion);
        Assert.True(result.Pending);
    }

    [Fact]
    public async Task Mapping_WhenContentMatches_KeepsCoreVersionWithoutPublication()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        fixture.Client.Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(version: 7, rules: []));
        var context = Input();
        await Synchronize(session, fixture, context);

        // Act
        await new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        var result = Assert.Single(context.MappingResults!);
        Assert.Equal(7, result.Contract!.ContractVersion);
        Assert.Null(result.TargetVersion);
        Assert.False(result.Pending);
    }

    [Fact]
    public async Task Mapping_WhenVersionOverflows_DoesNotCommitPartialResult()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        fixture.Client.Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(version: int.MaxValue));
        var context = Input();
        await Synchronize(session, fixture, context);

        // Act
        await Assert.ThrowsAsync<OverflowException>(() => new ContractMappingModule().ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.MappingResults);
        Assert.Null(context.Contracts);
    }

    [Fact]
    public async Task Persistence_WhenEndpointRemoved_RemovesOnlyPreviouslyScannedContracts()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        var persistence = new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock);
        var context = Input();
        await Synchronize(session, fixture, context);
        await new ContractMappingModule().ExecuteAsync(context, default);
        await persistence.ExecuteAsync(context, default);
        await session.Contracts.SetAsync(CreateContract("external"));
        await session.Contracts.SetAsync(CreateContract());

        // Act
        await persistence.ExecuteAsync(new ScanningContext { MappingResults = [] }, default);

        // Assert
        Assert.Null(session.Contracts.Get("products"));
        Assert.NotNull(session.Contracts.Get("external"));
        Assert.Empty(session.Context.ScannedContracts);
    }

    [Fact]
    public async Task Persistence_WhenMappingHasNotCompleted_DoesNotDeleteState()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        await session.Contracts.SetAsync(CreateContract());

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ContractPersistenceModule(session.Contracts, session.Access, fixture.Clock)
                .ExecuteAsync(new ScanningContext(), default));

        // Assert
        Assert.NotNull(session.Contracts.Get("products"));
    }
    
    [Theory]
    [InlineData("null")]
    [InlineData("endpoints")]
    [InlineData("preference")]
    public async Task ExecuteAsync_WhenInputIsInvalid_DoesNotRequestCore(string invalid)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
    
        var preference = invalid == "preference"
            ? (ContractSourcePreference)999
            : ContractSourcePreference.Application;
    
        var module = new ContractSynchronizationModule(store.Object, refresher.Object,
            Options.Create(new ContractSynchronizationOptions { SourcePreference = preference }),
            NullLogger<ContractSynchronizationModule>.Instance);
    
        var context = invalid switch
        {
            "null" => null,
            "endpoints" => new ScanningContext(),
            _ => new ScanningContext { Endpoints = [Endpoint()] }
        };
    
        // Act
        var action = () => module.ExecuteAsync(context!, default);
    
        // Assert
        if (invalid == "null")
            await Assert.ThrowsAsync<ArgumentNullException>(action);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(action);
    
        refresher.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenRefresherReturnsWrongSet_DoesNotCommitContext(bool sameCount)
    {
        // Arrange
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
    
        refresher
            .Setup(value => value.RefreshManyAsync(It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(sameCount ? new ContractRefreshResult[] { new() { Slug = "other" } } : Array.Empty<ContractRefreshResult>());
    
        var module = new ContractSynchronizationModule(store.Object, refresher.Object,
            Options.Create(new ContractSynchronizationOptions()),
            NullLogger<ContractSynchronizationModule>.Instance);
    
        var context = new ScanningContext { Endpoints = [Endpoint()] };
    
        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            module.ExecuteAsync(context, default));
    
        // Assert
        Assert.Null(context.RefreshedContracts);
        Assert.Null(context.CachedContracts);
    }
    
    [Theory]
    [InlineData("before")]
    [InlineData("between")]
    [InlineData("after")]
    public async Task ExecuteAsync_WhenCanceled_DoesNotCommitSynchronization(string moment)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
    
        var store = new Mock<IContractStore>();
        var refresher = new Mock<IContractRefresher>();
    
        var context = new ScanningContext
        {
            Endpoints =
            [
                Endpoint("first"),
                Endpoint("second")
            ]
        };
    
        if (moment == "before")
            cancellation.Cancel();
    
        if (moment == "between")
        {
            store
                .Setup(value => value.Get("first"))
                .Callback(() => cancellation.Cancel())
                .Returns((PublishedContract?)null);
        }
    
        refresher
            .Setup(value => value.RefreshManyAsync(It.IsAny<IReadOnlyCollection<string>>(), cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
    
                return Task.FromResult<IReadOnlyCollection<ContractRefreshResult>>(
                [
                    new() { Slug = "first" },
                    new() { Slug = "second" }
                ]);
            });
    
        var module = new ContractSynchronizationModule(store.Object, refresher.Object,
            Options.Create(new ContractSynchronizationOptions()),
            NullLogger<ContractSynchronizationModule>.Instance);
    
        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            module.ExecuteAsync(context, cancellation.Token));
    
        // Assert
        Assert.Null(context.RefreshedContracts);
        Assert.Null(context.CachedContracts);
    
        if (moment != "after")
            refresher.VerifyNoOtherCalls();
    }

    private static ScanningContext Input() => new()
    {
        Endpoints = [Endpoint()],
        Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = JsonNode.Parse("""{"type":"object"}""")!
        }
    };

    private static Task Synchronize(MicroDbSession session, MicroDbFixture fixture, ScanningContext context,
        ContractSourcePreference preference = ContractSourcePreference.Application) =>
        new ContractSynchronizationModule(
            session.Contracts,
            new ContractRefresher(session.Contracts, fixture.Client.Object),
            Options.Create(new ContractSynchronizationOptions { SourcePreference = preference }),
            NullLogger<ContractSynchronizationModule>.Instance).ExecuteAsync(context, default);
}

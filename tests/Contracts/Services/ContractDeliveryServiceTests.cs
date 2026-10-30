using System.Net;
using ContractWatcher.Common.Contracts;
using ContractWatcher.MicroDb.Exceptions;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
// ReSharper disable AccessToDisposedClosure

namespace ContractWatcher.SDK.Tests.Contracts.Services;

public sealed class ContractDeliveryServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenNewContract_PublishesAndPersistsConfirmation()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        using (var delivery = new ContractPublicationFixture(fixture))
        {
            await delivery.SeedAsync();

            // Act
            await delivery.Delivery.RunOnceAsync(default);

            // Assert
            fixture.Client.Verify(client => client.PublishContractAsync(It.Is<PublishedContract>(contract => 
                contract.Slug == "products" && contract.ContractVersion == 1), default), Times.Once);

            Assert.False(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
            Assert.Empty(delivery.Session.Context.ContractRetries);
        }

        using var reopened = fixture.Open();

        Assert.Equal(1, reopened.Contracts.Get("products")!.ContractVersion);
    }

    [Fact]
    public async Task RunOnceAsync_WhenResponseLost_ReconcilesWithoutSecondPostAfterRestart()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        PublishedContract? accepted = null;

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(() => accepted);

        fixture.Client
            .Setup(client => client.PublishContractAsync(It.IsAny<PublishedContract>(), default))
            .Callback<PublishedContract, CancellationToken>((contract, _) => accepted = contract)
            .ThrowsAsync(new HttpRequestException("Response lost"));

        using (var delivery = new ContractPublicationFixture(fixture))
        {
            await delivery.SeedAsync();
            await delivery.Delivery.RunOnceAsync(default);

            Assert.Equal(ScanningStage.Publication, delivery.Session.Context.ContractRetries.Find("products")!.Stage);
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        using var reopened = new ContractPublicationFixture(fixture);

        await reopened.Delivery.RunOnceAsync(default);

        // Assert
        fixture.Client.Verify(client => client.PublishContractAsync(
            It.IsAny<PublishedContract>(), default), Times.Once);

        Assert.False(reopened.Session.Context.ScannedContracts.Find("products")!.Pending);
        Assert.Empty(reopened.Session.Context.ContractRetries);
        Assert.Equal(1, reopened.Session.Contracts.Get("products")!.ContractVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunOnceAsync_WhenLocalPersistenceFails_ResumesPersistenceWithoutHttp(bool wrapped)
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        Exception failure = wrapped ? 
            new MicroDbException("Storage error", new IOException("Disk unavailable"))
            : new IOException("Disk unavailable");

        var store = new Mock<IContractStore>();

        store
            .Setup(value => 
                value.SetAsync(It.IsAny<PublishedContract>(), default))
            .ThrowsAsync(failure);

        using (var delivery = new ContractPublicationFixture(
                   fixture,
                   store.Object))
        {
            await delivery.SeedAsync();
            await delivery.Delivery.RunOnceAsync(default);

            Assert.Equal(
                ScanningStage.Persistence,
                delivery.Session.Context.ContractRetries
                    .Find("products")!.Stage);
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Client.Invocations.Clear();

        // Act
        using var reopened = new ContractPublicationFixture(fixture);

        await reopened.Delivery.RunOnceAsync(default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();

        Assert.Equal(1, reopened.Session.Contracts.Get("products")!.ContractVersion);
        Assert.Empty(reopened.Session.Context.ContractRetries);
    }

    [Fact]
    public async Task RunOnceAsync_WhenCoreOffline_ResumesSynchronizationAfterRestart()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(new HttpRequestException("Offline"));

        using (var delivery = new ContractPublicationFixture(fixture))
        {
            await delivery.SeedAsync(version: null);
            await delivery.Delivery.RunOnceAsync(default);

            Assert.Equal(ScanningStage.Synchronization, delivery.Session.Context.ContractRetries.Find("products")!.Stage);
        }

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(version: 7));

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        using var reopened = new ContractPublicationFixture(fixture);

        await reopened.Delivery.RunOnceAsync(default);

        // Assert
        fixture.Client.Verify(client => 
            client.PublishContractAsync(It.Is<PublishedContract>(contract => 
                contract.ContractVersion == 8), default), Times.Once);

        Assert.Equal(
            8,
            reopened.Session.Contracts.Get("products")!.ContractVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunOnceAsync_WhenRetryNotAllowed_StopsWithoutDroppingIntent(bool permanent)
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        Exception failure = permanent ? 
            new CoreRequestException(CoreRequestError.Unauthorized, "Unauthorized", HttpStatusCode.Unauthorized)
            : new HttpRequestException("Offline");

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(failure);

        using var delivery = new ContractPublicationFixture(fixture, attempts: 2);

        await delivery.SeedAsync(version: null);

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));

        await delivery.Delivery.RunOnceAsync(default);

        fixture.Clock.Advance(TimeSpan.FromHours(1));

        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        var retry = delivery.Session.Context.ContractRetries
            .Find("products")!;

        Assert.Null(retry.NextAttemptAt);
        Assert.Equal(permanent ? 1 : 2, retry.FailedAttempts);
        Assert.True(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);

        fixture.Client.Verify(client => 
            client.GetContractAsync("products", default), Times.Exactly(permanent ? 1 : 2));
    }

    [Fact]
    public async Task RunOnceAsync_WhenPreferencePublished_DoesNotSendOldPendingIntent()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        using var delivery = new ContractPublicationFixture(
            fixture, preference: ContractSourcePreference.Published);

        await delivery.SeedAsync();

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TrySendAsync_WhenConcurrent_SendsOnce()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Client
            .Setup(client => client.PublishContractAsync(It.IsAny<PublishedContract>(), default))
            .Returns(async (PublishedContract contract, CancellationToken _) =>
            {
                entered.TrySetResult();

                await release.Task;

                return contract;
            });

        // Act
        var first = delivery.Delivery.TrySendAsync("products", default);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var second = delivery.Delivery.TrySendAsync("products", default);

        release.TrySetResult();

        await Task.WhenAll(first, second);

        // Assert
        fixture.Client.Verify(client => client.PublishContractAsync(It.IsAny<PublishedContract>(), default), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_WhenCanceled_DoesNotCountCancellationAsFailure()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);
        using var cancellation = new CancellationTokenSource();

        await delivery.SeedAsync();

        fixture.Client
            .Setup(client => client.GetContractAsync("products", cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();

                return Task.FromCanceled<PublishedContract?>(cancellation.Token);
            });

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delivery.Delivery.RunOnceAsync(cancellation.Token));

        // Assert
        Assert.Equal(0, delivery.Session.Context.ContractRetries.Find("products")!.FailedAttempts);

        Assert.True(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
    }

    [Fact]
    public async Task Persistence_WhenRestartScanHasUnknownVersion_PreservesAcknowledgedCheckpoint()
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        var store = new Mock<IContractStore>();

        store
            .Setup(value => 
                value.SetAsync(It.IsAny<PublishedContract>(), default))
            .ThrowsAsync(new IOException("Disk unavailable"));

        using (var first = new ContractPublicationFixture(fixture, store.Object))
        {
            await first.SeedAsync();
            await first.Delivery.RunOnceAsync(default);
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Client.Invocations.Clear();

        using var second = new ContractPublicationFixture(fixture);

        var module = new ContractPersistenceModule(
            second.Session.Contracts, second.Session.Access, fixture.Clock, second.Gate);

        var context = new ScanningContext
        {
            MappingResults =
            [
                new ContractMappingResult
                {
                    Slug = "products",
                    Rules = [],
                    Pending = true
                }
            ]
        };

        // Act
        await module.ExecuteAsync(context, default);
        await second.Delivery.RunOnceAsync(default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();

        Assert.Equal(1, second.Session.Contracts.Get("products")!.ContractVersion);
        Assert.Empty(second.Session.Context.ContractRetries);
    }

    [Fact]
    public async Task RunOnceAsync_WhenOneContractFails_ContinuesOtherContracts()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync("broken");
        await delivery.SeedAsync("healthy");

        fixture.Client
            .Setup(client => 
                client.PublishContractAsync(It.Is<PublishedContract>(contract => 
                    contract.Slug == "broken"), default))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.ServerError, "Unavailable", HttpStatusCode.ServiceUnavailable));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.True(delivery.Session.Context.ScannedContracts.Find("broken")!.Pending);
        Assert.False(delivery.Session.Context.ScannedContracts.Find("healthy")!.Pending);
        Assert.NotNull(delivery.Session.Context.ContractRetries.Find("broken")!.NextAttemptAt);
    }

    [Theory]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(408)]
    public async Task RunOnceAsync_WhenCoreReturnsRetryableStatus_SchedulesNextAttempt(int status)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        fixture.Client
            .Setup(client => client.PublishContractAsync(It.IsAny<PublishedContract>(), default))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.UnexpectedResponse, "Retry", (HttpStatusCode)status));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Equal(fixture.Clock.GetUtcNow().AddSeconds(1), delivery.Session.Context.ContractRetries
            .Find("products")!.NextAttemptAt);
    }

    [Theory]
    [InlineData("wrong-slug")]
    [InlineData("zero-version")]
    [InlineData("overflow")]
    public async Task RunOnceAsync_WhenCoreContractIsInvalid_StopsWithoutPublishing(string fault)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var slug = fault == "wrong-slug" ? "other" : "products";
        var version = fault == "zero-version" ? 0 : fault == "overflow" ? int.MaxValue : 1;

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(slug, version));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Null(delivery.Session.Context.ContractRetries.Find("products")!.NextAttemptAt);

        fixture.Client.Verify(client => 
            client.PublishContractAsync(It.IsAny<PublishedContract>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public async Task TrySendAsync_WhenPersistenceCheckpointHasInvalidVersion_RecordsPermanentFailure(int? version)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync(version: version);

        var entity = delivery.Session.Context.ScannedContracts.Find("products")!;

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "products",
                Revision = entity.Revision,
                Stage = ScanningStage.Persistence,
                NextAttemptAt = fixture.Clock.GetUtcNow()
            });

        // Act
        await delivery.Delivery.TrySendAsync("products", default);

        // Assert
        Assert.Null(delivery.Session.Context.ContractRetries.Find("products")!.NextAttemptAt);

        Assert.True(entity.Pending);

        fixture.Client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_WhenCheckpointStageIsInvalid_StopsAndKeepsIntent()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "products",
                Revision = delivery.Session.Context.ScannedContracts.Find("products")!.Revision,
                Stage = ScanningStage.Schema,
                NextAttemptAt = fixture.Clock.GetUtcNow()
            });

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        var retry = delivery.Session.Context.ContractRetries.Find("products")!;

        Assert.Null(retry.NextAttemptAt);
        Assert.Contains("Unsupported delivery stage", retry.LastError);

        fixture.Client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrySendAsync_WhenCheckpointIsNotDue_DoesNotSend(bool stopped)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "products",
                Revision = delivery.Session.Context.ScannedContracts.Find("products")!.Revision,
                NextAttemptAt = stopped ? null : fixture.Clock.GetUtcNow().AddDays(1)
            });

        // Act
        await delivery.Delivery.TrySendAsync("products", default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TrySendAsync_WhenNoIntentOrPublishedPreference_DoesNothing()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        // Act
        await delivery.Delivery.TrySendAsync("missing", default);

        using var published = new ContractPublicationFixture(
            fixture, preference: ContractSourcePreference.Published);

        await published.Delivery.TrySendAsync("missing", default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_WhenCheckpointBelongsToOldRevision_ReplacesItAndCleansOrphans()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "products",
                Revision = Guid.NewGuid()
            });

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "removed",
                Revision = Guid.NewGuid()
            });

        await delivery.SeedAsync("completed");

        delivery.Session.Context.ScannedContracts.Find("completed")!.Pending = false;

        delivery.Session.Context.ContractRetries.Add(
            new ContractRetryEntity
            {
                Slug = "completed",
                Revision = Guid.NewGuid()
            });

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Empty(delivery.Session.Context.ContractRetries);
        Assert.False(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
    }

    [Fact]
    public async Task RunOnceAsync_WhenLocalVersionIsNewer_DoesNotDowngradeIt()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();
        await delivery.Session.Contracts.SetAsync(CreateContract(version: 9));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Equal(9, delivery.Session.Contracts.Get("products")!.ContractVersion);
        Assert.False(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunOnceAsync_WhenUnlimitedAttempts_RetriesTimeoutsBeyondFive(bool canceledInternally)
    {
        // Arrange
        using var fixture = new MicroDbFixture();

        using var delivery = new ContractPublicationFixture(fixture, attempts: 0);

        await delivery.SeedAsync(version: null);

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(canceledInternally ? new OperationCanceledException() : new TimeoutException());

        // Act
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await delivery.Delivery.RunOnceAsync(default);
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        var retry = delivery.Session.Context.ContractRetries.Find("products")!;

        Assert.Equal(6, retry.FailedAttempts);
        Assert.NotNull(retry.NextAttemptAt);
    }

    [Theory]
    [InlineData((int)CoreRequestError.NetworkError)]
    [InlineData((int)CoreRequestError.Timeout)]
    public async Task RunOnceAsync_WhenCoreTransportFails_SchedulesRetry(int error)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(new CoreRequestException((CoreRequestError)error, "Temporary failure"));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.NotNull(delivery.Session.Context.ContractRetries.Find("products")!.NextAttemptAt);
    }

    [Fact]
    public async Task RunOnceAsync_WhenCheckpointSaveFails_RecoversWithoutRepeatingAcceptedPost()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var path = Path.Combine(fixture.DirectoryPath, "contract-retries.json");

        fixture.Client
            .Setup(client => client.PublishContractAsync(It.IsAny<PublishedContract>(), default))
            .Returns((PublishedContract contract, CancellationToken _) =>
            {
                // Core принял контракт, но сохранение подтверждения упадёт.
                File.Move(path, path + ".saved");
                Directory.CreateDirectory(path);

                return Task.FromResult(contract);
            });

        // Act
        try
        {
            await Assert.ThrowsAsync<MicroDbException>(
                () => delivery.Delivery.RunOnceAsync(default));
        }
        finally
        {
            Directory.Delete(path);
            File.Move(path + ".saved", path);
        }

        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.False(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
        Assert.Empty(delivery.Session.Context.ContractRetries);

        fixture.Client.Verify(client => 
            client.PublishContractAsync(It.IsAny<PublishedContract>(), default), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_WhenCustomStoreRemovesCheckpointBeforeFailure_DoesNotMaskOriginalFailure()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        var store = new Mock<IContractStore>();
        using var delivery = new ContractPublicationFixture(fixture, store.Object);

        await delivery.SeedAsync();

        store
            .Setup(value => value.SetAsync(It.IsAny<PublishedContract>(), default))
            .Returns(() =>
            {
                delivery.Session.Context.ContractRetries.Remove("products");

                throw new IOException("Custom storage failed");
            });

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.True(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
        Assert.Empty(delivery.Session.Context.ContractRetries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunOnceAsync_WhenErrorHasNoTransientCause_StopsRetry(bool storage)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        Exception error = storage ? new MicroDbException("Invalid data")
            : new CoreRequestException(CoreRequestError.UnexpectedResponse, "Invalid response");

        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ThrowsAsync(error);

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Null(delivery.Session.Context.ContractRetries.Find("products")!.NextAttemptAt);
    }
    
    [Fact]
    public async Task RunOnceAsync_WhenCanceledBetweenContracts_DoesNotProcessNextContract()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var cancellation = new CancellationTokenSource();
    
        var logger = new Mock<ILogger<ContractDeliveryService>>();
    
        // Предупреждение записывается после сохранения ошибки первого контракта.
        // Отмена произойдёт до перехода ко второму элементу очереди.
        logger
            .Setup(value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(_ => cancellation.Cancel()));
    
        using var delivery = new ContractPublicationFixture(fixture, logger: logger.Object);
    
        await delivery.SeedAsync("first");
        await delivery.SeedAsync("second");
    
        fixture.Client
            .Setup(client => client.GetContractAsync("first", cancellation.Token))
            .ThrowsAsync(new HttpRequestException("Offline"));
    
        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delivery.Delivery.RunOnceAsync(cancellation.Token));
    
        // Assert
        Assert.Equal(1, delivery.Session.Context.ContractRetries.Find("first")!.FailedAttempts);
        Assert.Null(delivery.Session.Context.ContractRetries.Find("second"));
        Assert.True(delivery.Session.Context.ScannedContracts.Find("second")!.Pending);
    
        fixture.Client.Verify(client => 
            client.GetContractAsync("first", cancellation.Token), Times.Once);
    
        fixture.Client.Verify(client => 
            client.GetContractAsync("second", It.IsAny<CancellationToken>()), Times.Never);
    
        fixture.Client.Verify(client => 
            client.PublishContractAsync(It.IsAny<PublishedContract>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunOnceAsync_WhenCoreAssignsDifferentVersion_PersistsItAcrossRestart(bool localFailure)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
    
        fixture.Client
            .Setup(client => client.GetContractAsync("products", default))
            .ReturnsAsync(CreateContract(version: 1));
    
        fixture.Client
            .Setup(client => 
                client.PublishContractAsync(It.IsAny<PublishedContract>(), default))
            .ReturnsAsync((PublishedContract contract, CancellationToken _) =>
                new PublishedContract
                {
                    Slug = contract.Slug,
                    Rules = contract.Rules,
                    ContractVersion = 12
                });
    
        var store = new Mock<IContractStore>();
    
        store
            .Setup(value => 
                value.SetAsync(It.IsAny<PublishedContract>(), default))
            .ThrowsAsync(new IOException("Unavailable"));
    
        using (var first = new ContractPublicationFixture(fixture, localFailure ? store.Object : null))
        {
            await first.SeedAsync(version: 2);
    
            // Act
            await first.Delivery.RunOnceAsync(default);
    
            // Assert
            Assert.Equal(12, first.Session.Context.ScannedContracts.Find("products")!.TargetVersion);
    
            fixture.Client.Verify(client => 
                client.PublishContractAsync(It.Is<PublishedContract>(contract => 
                    contract.ContractVersion == 2), default), Times.Once);
        }
    
        // Arrange: повторно открываем ту же базу.
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Client.Invocations.Clear();
    
        using var reopened = new ContractPublicationFixture(fixture);
    
        // Act
        await reopened.Delivery.RunOnceAsync(default);
    
        // Assert: подтверждённая версия восстановлена без повторного HTTP-запроса.
        fixture.Client.VerifyNoOtherCalls();
    
        Assert.Equal(12, reopened.Session.Contracts.Get("products")!.ContractVersion);
        Assert.Empty(reopened.Session.Context.ContractRetries);
    }
    
    [Fact]
    public async Task RunOnceAsync_WhenPublishedRulesDiffer_DoesNotConfirmContract()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        // SeedAsync сохраняет пустые правила.
        // CreateContract возвращает контракт с правилом id.
        fixture.Client
            .Setup(client => 
                client.PublishContractAsync(It.IsAny<PublishedContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateContract(version: 12));

        // Act
        await delivery.Delivery.RunOnceAsync(default);

        // Assert
        Assert.Null(delivery.Session.Contracts.Get("products"));

        var scanned = delivery.Session.Context.ScannedContracts.Find("products")!;

        Assert.True(scanned.Pending);
        Assert.Equal(1, scanned.TargetVersion);

        var retry = delivery.Session.Context.ContractRetries.Find("products")!;

        Assert.Equal(ScanningStage.Publication, retry.Stage);
        Assert.Equal(1, retry.FailedAttempts);
        Assert.Null(retry.NextAttemptAt);
        Assert.Equal("Core returned different rules after publication.", retry.LastError);

        fixture.Client.Verify(client => 
            client.PublishContractAsync(It.IsAny<PublishedContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
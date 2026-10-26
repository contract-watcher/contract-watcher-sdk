using System.Net;
using ContractWatcher.Common.Contracts;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Enums;
using ContractWatcher.SDK.Data.Mappings;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.Time.Testing;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Reporting.Services;

public sealed class ReportDeliveryServiceTests : IDisposable
{
    private readonly MicroDbFixture _fixture = new();

    private string _directory => _fixture.DirectoryPath;
    private FakeTimeProvider _clock => _fixture.Clock;
    private Mock<IContractWatcherCoreClient> _client => _fixture.Client;

    private MicroDbSession Open(int maxAttempts = 0) => _fixture.Open(maxAttempts);

    [Fact]
    public async Task PublishAsync_WhenCoreAccepts_StoresDeliveredReportBeforeRemovingRetry()
    {
        // Arrange
        var report = CreateReport();

        using (var session = Open())
        {
            // Act
            await session.Delivery.PublishAsync(report, default);

            // Assert
            _client.Verify(client => client.PublishReportAsync(
                It.Is<ValidationReport>(value => value.ReportId == report.ReportId), default), Times.Once);
        }

        using var reopened = Open();

        Assert.Equal(ReportDeliveryState.Delivered, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);
        Assert.Empty(reopened.Context.ReportRetries);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task PublishAsync_WhenCoreUnavailable_RestoresRetryAfterRestartAndHonorsDelay(int status)
    {
        // Arrange
        var report = CreateReport();

        var error = status >= 500 ? CoreRequestError.ServerError : CoreRequestError.UnexpectedResponse;

        _client
            .SetupSequence(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CoreRequestException(error, "Unavailable", (HttpStatusCode)status))
            .Returns(Task.CompletedTask);

        using (var session = Open())
        {
            // Act
            await session.Delivery.PublishAsync(report, default);

            // Assert
            Assert.Equal(ReportDeliveryState.Pending, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
            Assert.Equal(1, session.Context.ReportRetries.Find(report.ReportId)!.FailedAttempts);
        }

        using var reopened = Open();

        // Срок повторной отправки ещё не наступил.
        await reopened.Worker.RunOnceAsync(default);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Once);

        // Продвигаем виртуальное время за предел максимальной задержки.
        _clock.Advance(TimeSpan.FromSeconds(3));

        await reopened.Worker.RunOnceAsync(default);

        Assert.Equal(ReportDeliveryState.Delivered, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);
        Assert.Empty(reopened.Context.ReportRetries);

        _client.Verify(client => client.PublishReportAsync(
            It.Is<ValidationReport>(value =>
                value.ReportId == report.ReportId &&
                value.Timestamp == report.Timestamp),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task PublishAsync_WhenPermanentError_PersistsFailedAndDoesNotRetry(int status)
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();

        _client
            .Setup(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.UnexpectedResponse, "Rejected", (HttpStatusCode)status));

        // Act
        await Assert.ThrowsAsync<CoreRequestException>(() => 
            session.Delivery.PublishAsync(report, default));

        _clock.Advance(TimeSpan.FromDays(1));

        await session.Worker.RunOnceAsync(default);

        // Assert
        Assert.Equal(ReportDeliveryState.Failed, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
        Assert.Null(session.Context.ReportRetries.Find(report.ReportId)!.NextAttemptAt);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenCanceledDuringHttp_PreservesPendingWithoutFailedAttempt()
    {
        // Arrange
        using var session = Open();
        using var cancellation = new CancellationTokenSource();

        var report = CreateReport();

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();

                return Task.FromCanceled(cancellation.Token);
            });

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            session.Delivery.PublishAsync(report, cancellation.Token));

        // Assert
        Assert.Equal(ReportDeliveryState.Pending, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
        Assert.Empty(session.Context.ReportRetries);
    }

    [Fact]
    public async Task PublishAsync_WhenSaveFails_DoesNotSendUntilPersistenceRecovers()
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();
        var path = Path.Combine(_directory, "reports.json");

        // Каталог на месте файла не позволяет MicroDb сохранить отчёты.
        Directory.CreateDirectory(path);

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => session.Delivery.PublishAsync(report, default));
        await Assert.ThrowsAnyAsync<Exception>(() => session.Worker.RunOnceAsync(default));

        // Assert
        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Never);

        Directory.Delete(path);

        await session.Worker.RunOnceAsync(default);

        Assert.Equal(ReportDeliveryState.Delivered, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
    }

    [Fact]
    public async Task PublishAsync_WhenRetryLimitReached_PersistsFailed()
    {
        // Arrange
        using var session = Open(maxAttempts: 1);
        var report = CreateReport();

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .ThrowsAsync(new HttpRequestException("Offline"));

        // Act
        await Assert.ThrowsAsync<HttpRequestException>(() => session.Delivery.PublishAsync(report, default));

        // Assert
        Assert.Equal(ReportDeliveryState.Failed, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("http")]
    [InlineData("canceled-by-client")]
    [InlineData("core-timeout")]
    public async Task PublishAsync_WhenTransportFails_PreservesPendingAndSchedulesRetry(string kind)
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();

        Exception exception = kind switch
        {
            "network" => new CoreRequestException(CoreRequestError.NetworkError, "Offline"),
            "timeout" => new TimeoutException("Timeout"),
            "http" => new HttpRequestException("Offline"),
            "canceled-by-client" => new OperationCanceledException("Transport canceled"),

            _ => new CoreRequestException(CoreRequestError.Timeout, "Timeout")
        };

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .ThrowsAsync(exception);

        // Act
        await session.Delivery.PublishAsync(report, default);

        // Assert
        Assert.Equal(ReportDeliveryState.Pending, session.Context.Reports.Find(report.ReportId)!.DeliveryState);

        var retry = session.Context.ReportRetries.Find(report.ReportId)!;

        Assert.Equal(1, retry.FailedAttempts);
        Assert.Equal(exception.Message, retry.LastError);

        Assert.InRange(
            retry.NextAttemptAt!.Value - _clock.GetUtcNow(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1.2));
    }

    [Fact]
    public async Task PublishAsync_WhenReportIdIsEmpty_DoesNotStoreOrSend()
    {
        // Arrange
        using var session = Open();

        var report = new ValidationReport
        {
            ReportId = Guid.Empty,
            Slug = "test",
            ContractVersion = 1,
            Status = ValidationStatus.Passed,
            Violations = []
        };

        // Act
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.Delivery.PublishAsync(report, default));

        // Assert
        Assert.Empty(session.Context.Reports);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenPayloadChangesUnderSameId_RejectsReplacement()
    {
        // Arrange
        using var session = Open();
        var original = CreateReport();

        await session.Delivery.PublishAsync(original, default);

        var changed = original.ToEntity(_clock.GetUtcNow());
        changed.ContractVersion++;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.Delivery.PublishAsync(changed.ToDto(), default));

        // Assert
        Assert.Equal(original.ContractVersion, session.Context.Reports.Find(original.ReportId)!.ContractVersion);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenDeliveredReportIsPublishedAgain_DoesNotSendAgain()
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();

        await session.Delivery.PublishAsync(report, default);

        // Act
        await session.Delivery.PublishAsync(report, default);

        // Assert
        Assert.Single(session.Context.Reports);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Once);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("delivered")]
    [InlineData("failed")]
    [InlineData("disabled")]
    [InlineData("future")]
    public async Task TrySendAsync_WhenReportIsNotEligible_DoesNotSend(string state)
    {
        // Arrange
        using var session = Open();
        var report = CreateReport().ToEntity(_clock.GetUtcNow());

        if (state != "missing")
            session.Context.Reports.Add(report);

        if (state == "delivered")
            report.DeliveryState = ReportDeliveryState.Delivered;

        if (state == "failed")
            report.DeliveryState = ReportDeliveryState.Failed;

        if (state is "disabled" or "future")
        {
            session.Context.ReportRetries.Add(new ReportRetryEntity
            {
                ReportId = report.ReportId,
                NextAttemptAt = state == "future" ? _clock.GetUtcNow().AddDays(1) : null
            });
        }

        await session.Context.SaveChangesAsync();

        // Act
        await session.Delivery.TrySendAsync(report.ReportId, default);

        // Assert
        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Never);
    }

    [Fact]
    public async Task GetDueAsync_WhenMoreReportsExist_ReturnsOldestDueBatch()
    {
        // Arrange
        using var session = Open();

        session.Settings.BatchSize = 2;

        var oldest = CreateReport().ToEntity(_clock.GetUtcNow());

        var second = CreateReport()
            .ToEntity(_clock.GetUtcNow().AddSeconds(1));

        var newest = CreateReport()
            .ToEntity(_clock.GetUtcNow().AddSeconds(2));

        session.Context.Reports.Add(newest);
        session.Context.Reports.Add(oldest);
        session.Context.Reports.Add(second);

        await session.Context.SaveChangesAsync();

        // Act
        var ids = await session.Delivery.GetDueAsync(default);

        // Assert
        Assert.Equal(new[] { oldest.ReportId, second.ReportId }, ids);
    }

    [Fact]
    public async Task TrySendAsync_WhenRetryFailsAgain_UpdatesSameEntityAndCapsDelay()
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .ThrowsAsync(new HttpRequestException("Offline"));

        await session.Delivery.PublishAsync(report, default);

        var retry = session.Context.ReportRetries.Find(report.ReportId)!;

        _clock.Advance(TimeSpan.FromSeconds(3));

        // Act
        await session.Delivery.TrySendAsync(report.ReportId, default);

        // Assert
        Assert.Same(retry, session.Context.ReportRetries.Find(report.ReportId));
        Assert.Equal(2, retry.FailedAttempts);
        Assert.Equal(_clock.GetUtcNow() + session.Settings.MaxDelay, retry.NextAttemptAt);

        using var reopened = Open();

        Assert.Equal(2, reopened.Context.ReportRetries.Find(report.ReportId)!.FailedAttempts);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_WhenErrorIsNotRetryable_PersistsFailureAndRethrows(bool coreError)
    {
        // Arrange
        using var session = _fixture.Open();
        var report = CreateReport();
    
        Exception failure = coreError ? 
            new CoreRequestException(CoreRequestError.UnexpectedResponse, "No status") : 
            new InvalidOperationException("Unexpected failure");
    
        _fixture.Client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .ThrowsAsync(failure);
    
        // Act
        var exception = await Record.ExceptionAsync(
            () => session.Delivery.PublishAsync(report, default));
    
        // Assert
        Assert.Same(failure, exception);
    
        Assert.Equal(ReportDeliveryState.Failed, session.Context.Reports.Find(report.ReportId)!.DeliveryState);
    
        var retry = session.Context.ReportRetries.Find(report.ReportId)!;
    
        Assert.Equal(1, retry.FailedAttempts);
        Assert.Equal(failure.Message, retry.LastError);
        Assert.Null(retry.NextAttemptAt);
    
        using var reopened = _fixture.Open();
    
        Assert.Equal(ReportDeliveryState.Failed, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);
    }
    
    [Fact]
    public async Task GetDueAsync_WhenRetryIsDisabled_ExcludesPendingReport()
    {
        // Arrange
        using var session = _fixture.Open();
        var report = CreateReport().ToEntity(_fixture.Clock.GetUtcNow());
    
        session.Context.Reports.Add(report);
        session.Context.ReportRetries.Add(new ReportRetryEntity
        {
            ReportId = report.ReportId,
            NextAttemptAt = null
        });
    
        await session.Context.SaveChangesAsync();
    
        // Act
        var result = await session.Delivery.GetDueAsync(default);
    
        // Assert
        Assert.Empty(result);
        Assert.Equal(ReportDeliveryState.Pending, report.DeliveryState);
    
        _fixture.Client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _fixture.Dispose();
}
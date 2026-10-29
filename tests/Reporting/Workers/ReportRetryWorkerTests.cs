using System.Net;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Enums;
using ContractWatcher.SDK.Data.Mappings;
using ContractWatcher.SDK.Reporting.Workers;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;
// ReSharper disable InconsistentNaming
// ReSharper disable AccessToDisposedClosure

namespace ContractWatcher.SDK.Tests.Reporting.Workers;

public sealed class ReportRetryWorkerTests : IDisposable
{
    private readonly MicroDbFixture _fixture = new();

    private string _directory => _fixture.DirectoryPath;
    private FakeTimeProvider _clock => _fixture.Clock;
    private Mock<IContractWatcherCoreClient> _client => _fixture.Client;

    private MicroDbSession Open() => _fixture.Open();

    [Fact]
    public async Task RunOnceAsync_WhenPendingReportHasNoRetry_RecoversAndSends()
    {
        // Arrange
        var report = CreateReport();

        using (var session = Open())
        {
            session.Context.Reports.Add(report.ToEntity(_clock.GetUtcNow()));

            await session.Context.SaveChangesAsync();
        }

        using var reopened = Open();

        // Act
        await reopened.Worker.RunOnceAsync(default);

        // Assert
        Assert.Equal(ReportDeliveryState.Delivered, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_WhilePublisherIsSending_DoesNotSendSameReportConcurrently()
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .Returns(async () =>
            {
                entered.TrySetResult();
                await release.Task;
            });

        // Act
        var publishing = session.Delivery.PublishAsync(report, default);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            await session.Worker.RunOnceAsync(default);
            await session.Delivery.PublishAsync(report, default);

            // Assert
            _client.Verify(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Once);
        }
        finally
        {
            release.TrySetResult();
        }

        await publishing;
    }

    [Fact]
    public async Task RunOnceAsync_AfterFailedConfirmationSave_FlushesWithoutAnotherHttpRequest()
    {
        // Arrange
        using var session = Open();
        var report = CreateReport();
        var path = Path.Combine(_directory, "reports.json");

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .Returns(() =>
            {
                // Core принял отчёт, но следующая запись на диск упадёт.
                File.Move(path, path + ".saved");
                Directory.CreateDirectory(path);

                return Task.CompletedTask;
            });

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => session.Delivery.PublishAsync(report, default));

        Directory.Delete(path);
        File.Move(path + ".saved", path);

        await session.Worker.RunOnceAsync(default);

        // Assert
        _client.Verify(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Once);

        using var reopened = Open();

        Assert.Equal(ReportDeliveryState.Delivered, reopened.Context.Reports.Find(report.ReportId)!.DeliveryState);
    }

    [Fact]
    public async Task CleanupAsync_WhenDeliveredReportHasRetry_RemovesRetryWithoutSending()
    {
        // Arrange
        using var session = Open();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());
        report.DeliveryState = ReportDeliveryState.Delivered;

        session.Context.Reports.Add(report);

        session.Context.ReportRetries.Add(new ReportRetryEntity
        {
            ReportId = report.ReportId
        });

        await session.Context.SaveChangesAsync();

        // Act
        await session.Worker.RunOnceAsync(default);

        // Assert
        Assert.Empty(session.Context.ReportRetries);

        _client.Verify(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WhenOneReportFailsPermanently_ContinuesWithOtherReports()
    {
        // Arrange
        using var session = Open();

        session.Context.Reports.Add(CreateReport().ToEntity(_clock.GetUtcNow()));
        session.Context.Reports.Add(CreateReport().ToEntity(_clock.GetUtcNow()));

        await session.Context.SaveChangesAsync();

        _client
            .SetupSequence(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), default))
            .ThrowsAsync(new CoreRequestException(CoreRequestError.UnexpectedResponse, "Bad report", HttpStatusCode.BadRequest))
            .Returns(Task.CompletedTask);

        // Act
        await session.Worker.RunOnceAsync(default);

        // Assert
        Assert.Single(session.Context.Reports, report => report.DeliveryState == ReportDeliveryState.Delivered);
        Assert.Single(session.Context.Reports, report => report.DeliveryState == ReportDeliveryState.Failed);
    }

    [Fact]
    public async Task RunOnceAsync_WhenCanceledInsideSend_PropagatesCancellation()
    {
        // Arrange
        using var session = Open();
        using var cancellation = new CancellationTokenSource();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());

        session.Context.Reports.Add(report);
        await session.Context.SaveChangesAsync();

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();

                return Task.FromCanceled(cancellation.Token);
            });

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.Worker.RunOnceAsync(cancellation.Token));

        // Assert
        Assert.Equal(ReportDeliveryState.Pending, report.DeliveryState);
        Assert.Empty(session.Context.ReportRetries);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStarted_ProcessesQueueAndStopsDuringDelay()
    {
        // Arrange
        using var session = Open();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());

        session.Context.Reports.Add(report);
        await session.Context.SaveChangesAsync();

        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client
            .Setup(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                sent.TrySetResult();

                return Task.CompletedTask;
            });

        // Act
        await session.Worker.StartAsync(default);

        try
        {
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await session.Worker.StopAsync(timeout.Token);
        }

        // Assert
        Assert.True(session.Worker.ExecuteTask!.IsCompleted);

        _client.Verify(client => 
            client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreRecovers_RetriesOnNextTimerTick()
    {
        // Arrange
        using var session = Open();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());

        session.Context.Reports.Add(report);
        await session.Context.SaveChangesAsync();

        _client
            .SetupSequence(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Offline"))
            .Returns(Task.CompletedTask);

        // Act
        await session.Worker.StartAsync(default);

        try
        {
            await _fixture.Clock.WaitForTimerAsync();

            Assert.Equal(ReportDeliveryState.Pending, report.DeliveryState);

            _clock.Advance(session.Settings.PollInterval);

            await _fixture.Clock.WaitForTimerAsync();

            // Assert
            Assert.Equal(ReportDeliveryState.Delivered, report.DeliveryState);
            Assert.Empty(session.Context.ReportRetries);

            _client.Verify(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            await session.Worker.StopAsync(default);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiskRecovers_ContinuesAfterFailedPass()
    {
        // Arrange
        using var session = Open();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());
        session.Context.Reports.Add(report);

        var path = Path.Combine(_directory, "reports.json");
        Directory.CreateDirectory(path);

        // Act
        await session.Worker.StartAsync(default);

        try
        {
            await _fixture.Clock.WaitForTimerAsync();

            _client.Verify(client => 
                client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()), Times.Never);

            Directory.Delete(path);

            _clock.Advance(session.Settings.PollInterval);

            await _fixture.Clock.WaitForTimerAsync();

            // Assert
            Assert.Equal(ReportDeliveryState.Delivered, report.DeliveryState);
        }
        finally
        {
            await session.Worker.StopAsync(default);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedDuringHttp_PreservesPendingReport()
    {
        // Arrange
        using var session = Open();

        var report = CreateReport().ToEntity(_clock.GetUtcNow());

        session.Context.Reports.Add(report);
        await session.Context.SaveChangesAsync();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .Returns<ValidationReport, CancellationToken>(async (_, token) =>
            {
                entered.TrySetResult();

                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        // Act
        await session.Worker.StartAsync(default);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await session.Worker.StopAsync(timeout.Token);
        }

        // Assert
        Assert.True(session.Worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(ReportDeliveryState.Pending, report.DeliveryState);
        Assert.Empty(session.Context.ReportRetries);
    }

    [Fact]
    public async Task CleanupAsync_WhenRetryHasNoReport_RemovesOrphan()
    {
        // Arrange
        using var session = Open();

        session.Context.ReportRetries.Add(new ReportRetryEntity
        {
            ReportId = Guid.NewGuid()
        });

        await session.Context.SaveChangesAsync();

        // Act
        var removed = await session.Delivery.CleanupAsync(default);

        // Assert
        Assert.Equal(1, removed);
        Assert.Empty(session.Context.ReportRetries);
    }
    
    [Fact]
    public async Task RunOnceAsync_WhenCanceledBetweenReports_DoesNotSendNextReport()
    {
        // Arrange
        using var session = Open();
        using var cancellation = new CancellationTokenSource();
    
        var first = CreateReport().ToEntity(_clock.GetUtcNow());
        var second = CreateReport().ToEntity(_clock.GetUtcNow().AddSeconds(1));
    
        session.Context.Reports.Add(first);
        session.Context.Reports.Add(second);
    
        await session.Context.SaveChangesAsync();
    
        _client
            .Setup(client => client.PublishReportAsync(It.IsAny<ValidationReport>(), cancellation.Token))
            .ThrowsAsync(new InvalidOperationException("Rejected"));
    
        var logger = new Mock<ILogger<ReportRetryWorker>>();
    
        // Отменяем выполнение после обработки ошибки первого отчёта,
        // чтобы следующий отчёт встретил отмену перед отправкой.
        logger
            .Setup(value => value.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(_ => cancellation.Cancel()));
    
        using var worker = new ReportRetryWorker(
            session.Delivery, Options.Create(session.Settings), _clock, logger.Object);
    
        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.RunOnceAsync(cancellation.Token));
    
        // Assert
        Assert.Equal(ReportDeliveryState.Failed, first.DeliveryState);
        Assert.Equal(ReportDeliveryState.Pending, second.DeliveryState);
        Assert.Null(session.Context.ReportRetries.Find(second.ReportId));
    
        _client.Verify(client => client.PublishReportAsync(
            It.Is<ValidationReport>(report => report.ReportId == first.ReportId), cancellation.Token), Times.Once);
    
        _client.Verify(client => client.PublishReportAsync(
            It.Is<ValidationReport>(report => report.ReportId == second.ReportId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _fixture.Dispose();
}
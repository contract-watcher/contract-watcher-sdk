using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Exceptions;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContractWatcher.SDK.Tests.Scanning.Workers;

public sealed class ContractScanningWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenApplicationStarts_ScansOnce()
    {
        // Arrange
        using var started = new CancellationTokenSource();

        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(value => value.ApplicationStarted).Returns(started.Token);

        var scanner = new Mock<IContractScanner>();
        scanner.Setup(value => value.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var services = new ServiceCollection();
        services.AddScoped<IContractScanner>(_ => scanner.Object);

        using var provider = services.BuildServiceProvider();

        using var worker = new ContractScanningWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime.Object,
            NullLogger<ContractScanningWorker>.Instance);

        // Act
        await worker.StartAsync(default);

        // Assert
        scanner.Verify(value => 
            value.ScanAsync(It.IsAny<CancellationToken>()), Times.Never);

        started.Cancel();

        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        scanner.Verify(value => 
            value.ScanAsync(It.IsAny<CancellationToken>()), Times.Once);

        lifetime.Verify(value => value.StopApplication(), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenScanningFails_LogsWithoutStoppingApplication(bool stageFailure)
    {
        // Arrange
        using var started = new CancellationTokenSource();
        started.Cancel();

        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(value => value.ApplicationStarted)
            .Returns(started.Token);

        Exception failure = stageFailure
            ? new ScanningException(ScanningStage.Schema, typeof(ContractScanningWorker), 5, "Request", new InvalidOperationException("Unsupported"))
            : new InvalidOperationException("Configuration failed");

        var scanner = new Mock<IContractScanner>();
        scanner.Setup(value => 
            value.ScanAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var logger = new Mock<ILogger<ContractScanningWorker>>();

        var services = new ServiceCollection();
        services.AddScoped<IContractScanner>(_ => scanner.Object);

        using var provider = services.BuildServiceProvider();

        using var worker = new ContractScanningWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime.Object,
            logger.Object);

        // Act
        await worker.StartAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        lifetime.Verify(value => value.StopApplication(), Times.Never);

        logger.Verify(
            value => value.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(
                    exception => ReferenceEquals(exception, failure)),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedBeforeApplicationStarted_DoesNotScan()
    {
        // Arrange
        var waiting = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var lifetime = new Mock<IHostApplicationLifetime>();

        lifetime.SetupGet(value => value.ApplicationStarted)
            .Callback(() => waiting.TrySetResult())
            .Returns(CancellationToken.None);

        var scanner = new Mock<IContractScanner>(MockBehavior.Strict);

        var services = new ServiceCollection();
        services.AddScoped<IContractScanner>(_ => scanner.Object);

        using var provider = services.BuildServiceProvider();

        using var worker = new ContractScanningWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime.Object,
            NullLogger<ContractScanningWorker>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await worker.StartAsync(default);
        await waiting.Task.WaitAsync(timeout.Token);
        await worker.StopAsync(timeout.Token);
        await worker.ExecuteTask!.WaitAsync(timeout.Token);

        // Assert
        scanner.VerifyNoOtherCalls();
    }
}
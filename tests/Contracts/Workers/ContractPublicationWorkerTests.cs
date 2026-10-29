using ContractWatcher.SDK.Contracts.Options;
using ContractWatcher.SDK.Contracts.Workers;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContractWatcher.SDK.Tests.Contracts.Workers;

public sealed class ContractPublicationWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenStartupIsIncompleteOrFailed_DoesNotSend(bool failed)
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var completion = new ScanningCompletion();

        if (failed)
            completion.Complete(false);

        using var worker = new ContractPublicationWorker(
            delivery.Delivery,
            completion,
            Options.Create(new ContractPublicationOptions()),
            fixture.Clock,
            NullLogger<ContractPublicationWorker>.Instance);

        // Act
        await worker.StartAsync(default);

        if (failed)
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        await worker.StopAsync(default);

        // Assert
        fixture.Client.VerifyNoOtherCalls();

        Assert.True(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiskRecovers_ContinuesQueueProcessing()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        delivery.Session.Context.ScannedContracts.Find("products")!.ScannedAt = fixture.Clock.GetUtcNow();

        var path = Path.Combine(fixture.DirectoryPath, "scanned-contracts.json");

        File.Move(path, path + ".saved");
        Directory.CreateDirectory(path);

        var completion = new ScanningCompletion();
        completion.Complete(true);

        using var worker = new ContractPublicationWorker(
            delivery.Delivery,
            completion,
            Options.Create(new ContractPublicationOptions()),
            fixture.Clock,
            NullLogger<ContractPublicationWorker>.Instance);

        // Act
        await worker.StartAsync(default);

        try
        {
            await fixture.Clock.WaitForTimerAsync();

            fixture.Client.VerifyNoOtherCalls();

            Directory.Delete(path);
            File.Move(path + ".saved", path);

            fixture.Clock.Advance(TimeSpan.FromSeconds(2));

            await fixture.Clock.WaitForTimerAsync();

            // Assert
            Assert.False(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
        }
        finally
        {
            await worker.StopAsync(default);

            if (Directory.Exists(path))
            {
                Directory.Delete(path);
                File.Move(path + ".saved", path);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedDuringHttp_LeavesCheckpointForRestart()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Client
            .Setup(client => client.GetContractAsync("products", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken token) =>
            {
                entered.TrySetResult();

                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return null;
            });

        var completion = new ScanningCompletion();
        completion.Complete(true);

        using var worker = new ContractPublicationWorker(
            delivery.Delivery,
            completion,
            Options.Create(new ContractPublicationOptions()),
            fixture.Clock,
            NullLogger<ContractPublicationWorker>.Instance);

        // Act
        await worker.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(delivery.Session.Context.ScannedContracts.Find("products")!.Pending);
        Assert.Equal(0, delivery.Session.Context.ContractRetries.Find("products")!.FailedAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedBeforeReadyLoop_DoesNotStartDelivery()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var delivery = new ContractPublicationFixture(fixture);

        await delivery.SeedAsync();

        var completion = new ScanningCompletion();
        completion.Complete(true);

        using var worker = new ContractPublicationWorker(
            delivery.Delivery,
            completion,
            Options.Create(new ContractPublicationOptions()),
            fixture.Clock,
            NullLogger<ContractPublicationWorker>.Instance);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await worker.StartAsync(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert
        fixture.Client.VerifyNoOtherCalls();
    }
}
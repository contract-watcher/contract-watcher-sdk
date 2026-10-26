using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Exceptions;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;

namespace ContractWatcher.SDK.Tests.Scanning;

public sealed class ContractScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenModulesFail_ResumesCurrentStageWithSameContext()
    {
        // Arrange
        var trace = new List<ScanningStage>();
        ScanningContext? initial = null;
        var schemaAttempts = 0;
        var mappingAttempts = 0;

        var discovery = new TestModule(
            ScanningStage.Discovery,
            (context, _) =>
            {
                initial = context;
                context.Endpoints = [Endpoint()];
                trace.Add(ScanningStage.Discovery);

                return Task.CompletedTask;
            });

        var schema = new TestModule(
            ScanningStage.Schema,
            (context, _) =>
            {
                Assert.Same(initial, context);
                Assert.Single(context.Endpoints!);
                Assert.Null(context.CurrentItem);

                context.CurrentItem = "Request";
                trace.Add(ScanningStage.Schema);

                if (++schemaAttempts < 3)
                    throw new IOException("Temporary schema failure");

                context.Schemas = new Dictionary<Type, System.Text.Json.Nodes.JsonNode>();

                return Task.CompletedTask;
            });

        var mapping = new TestModule(
            ScanningStage.Mapping,
            (context, _) =>
            {
                Assert.Same(initial, context);
                Assert.NotNull(context.Schemas);
                Assert.Null(context.CurrentItem);

                context.CurrentItem = "products";
                trace.Add(ScanningStage.Mapping);

                if (++mappingAttempts < 3)
                    throw new IOException("Temporary mapping failure");

                context.Contracts = [];

                return Task.CompletedTask;
            });

        var scanner = Scanner([discovery, schema, mapping], attempts: 3);

        // Act
        var contracts = await scanner.ScanAsync();

        // Assert
        Assert.Empty(contracts);

        Assert.Equal(
            new[]
            {
                ScanningStage.Discovery,
                ScanningStage.Schema,
                ScanningStage.Schema,
                ScanningStage.Schema,
                ScanningStage.Mapping,
                ScanningStage.Mapping,
                ScanningStage.Mapping
            },
            trace);
    }

    [Fact]
    public async Task ScanAsync_WhenAttemptsExhausted_PreservesFailureAndSkipsNextModule()
    {
        // Arrange
        var attempts = 0;
        var nextCalled = false;
        var failure = new InvalidOperationException("Unsupported DTO");

        var failing = new TestModule(
            ScanningStage.Schema,
            (context, _) =>
            {
                attempts++;
                context.CurrentItem = "CreateProductRequest";

                throw failure;
            });

        var next = new TestModule(ScanningStage.Mapping,
            (_, _) =>
            {
                nextCalled = true;

                return Task.CompletedTask;
            });

        var scanner = Scanner([failing, next]);

        // Act
        var exception = await Assert.ThrowsAsync<ScanningException>(() => scanner.ScanAsync());

        // Assert
        Assert.Equal(5, attempts);
        Assert.Equal(5, exception.Attempts);
        Assert.Equal(ScanningStage.Schema, exception.Stage);
        Assert.Equal(typeof(TestModule), exception.ModuleType);
        Assert.Equal("CreateProductRequest", exception.Item);
        Assert.Same(failure, exception.InnerException);
        Assert.False(nextCalled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScanAsync_WhenWaitingForRetry_RespectsDelayAndCancellation(bool cancel)
    {
        // Arrange
        var clock = new ScanningClock();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        var module = new TestModule(ScanningStage.Mapping,
            (context, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new IOException("Temporary failure");

                context.Contracts = [];

                return Task.CompletedTask;
            });

        var scanner = Scanner([module], delay: TimeSpan.FromSeconds(10), clock: clock);

        // Act
        var execution = scanner.ScanAsync(cancellation.Token);

        await clock.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        clock.Advance(TimeSpan.FromSeconds(9));

        // Assert
        Assert.False(execution.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref attempts));

        if (cancel)
        {
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.Equal(1, attempts);
        }
        else
        {
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(await execution.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(2, attempts);
        }
    }

    [Fact]
    public async Task ScanAsync_WhenTokenAlreadyCanceled_DoesNotExecuteModules()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var called = false;

        var scanner = Scanner(
        [
            new TestModule(ScanningStage.Discovery, (_, _) =>
            {
                called = true;

                return Task.CompletedTask;
            })
        ]);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(cancellation.Token));

        // Assert
        Assert.False(called);
    }

    [Fact]
    public async Task ScanAsync_WhenCalledAgain_CreatesNewContext()
    {
        // Arrange
        var contexts = new List<ScanningContext>();

        var scanner = Scanner(
        [
            new TestModule(ScanningStage.Mapping, (context, _) =>
            {
                contexts.Add(context);
                context.Contracts = [];

                return Task.CompletedTask;
            })
        ]);

        // Act
        await scanner.ScanAsync();
        await scanner.ScanAsync();

        // Assert
        Assert.Equal(2, contexts.Count);
        Assert.NotSame(contexts[0], contexts[1]);
    }

    [Fact]
    public async Task ScanAsync_WhenNoContractsProduced_RejectsIncompletePipeline()
    {
        // Arrange
        var scanner = Scanner([]);

        // Act
        var action = () => scanner.ScanAsync();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }
    
    [Fact]
    public async Task ScanAsync_WhenModuleCancelsToken_DoesNotRetryOrContinue()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        var scanner = Scanner(
        [
            new TestModule(ScanningStage.Schema, (_, _) =>
            {
                attempts++;
                cancellation.Cancel();

                return Task.CompletedTask;
            })
        ]);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(cancellation.Token));

        // Assert
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ScanAsync_WhenFailureHasNoItem_PreservesNullInException()
    {
        // Arrange
        var failure = new IOException("Unavailable");

        var scanner = Scanner(
            [new TestModule(ScanningStage.Schema, (_, _) => throw failure)], attempts: 1);

        // Act
        var exception = await Assert.ThrowsAsync<ScanningException>(
            () => scanner.ScanAsync());

        // Assert
        Assert.Null(exception.Item);
        Assert.Same(failure, exception.InnerException);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScanAsync_WhenCanceledBetweenStages_StopsExecution(bool beforeModule)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var calls = 0;

        IEnumerable<IScanningModule> Modules()
        {
            if (beforeModule)
                cancellation.Cancel();

            yield return new TestModule(
                ScanningStage.Mapping,
                (context, _) =>
                {
                    calls++;
                    context.Contracts = [];

                    return Task.CompletedTask;
                });

            if (!beforeModule)
                cancellation.Cancel();
        }

        var scanner = Scanner(Modules());

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(cancellation.Token));

        // Assert
        Assert.Equal(beforeModule ? 0 : 1, calls);
    }
}
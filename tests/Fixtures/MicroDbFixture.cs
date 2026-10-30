using ContractWatcher.Common.Contracts;
using ContractWatcher.MicroDb.Options;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Data;
using ContractWatcher.SDK.Data.Services;
using ContractWatcher.SDK.Reporting.Options;
using ContractWatcher.SDK.Reporting.Services;
using ContractWatcher.SDK.Reporting.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ContractWatcher.SDK.Tests.Fixtures;

/// <summary>
/// Изолирует файлы теста и сохраняет часы и клиент
/// при повторном открытии базы.
/// </summary>
public sealed class MicroDbFixture : IDisposable
{
    public string DirectoryPath { get; } = 
        Path.Combine(Path.GetTempPath(), "cw-sdk-tests", Guid.NewGuid().ToString("N"));

    public RetryTimeProvider Clock { get; } = new();

    public Mock<IContractWatcherCoreClient> Client { get; } = new();

    internal MicroDbSession Open(int maxAttempts = 0) => new(this, maxAttempts);
    
    public MicroDbFixture()
    {
        Client
            .Setup(client => 
                client.PublishContractAsync(It.IsAny<PublishedContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedContract contract, CancellationToken _) => contract);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, recursive: true);
    }
}

/// <summary>
/// Сеанс реальной MicroDb.
/// Освобождается до удаления каталога fixture.
/// </summary>
internal sealed class MicroDbSession : IDisposable
{
    private readonly ServiceProvider _provider;

    public ContractWatcherDbContext Context { get; }

    public DatabaseAccess Access { get; }

    public MicroDbContractStore Contracts { get; }

    public ReportDeliveryService Delivery { get; }

    public ReportRetryWorker Worker { get; }

    public ReportRetryOptions Settings { get; }

    public MicroDbSession(MicroDbFixture fixture, int maxAttempts)
    {
        Context = new ContractWatcherDbContext(new MicroDbOptions
        {
            DirectoryPath = fixture.DirectoryPath
        });

        Access = new DatabaseAccess(Context);
        Contracts = new MicroDbContractStore(Access, fixture.Clock);

        _provider = new ServiceCollection()
            .AddSingleton(fixture.Client.Object)
            .BuildServiceProvider();

        Settings = new ReportRetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(2),
            MaxAttempts = maxAttempts
        };

        var options = Options.Create(Settings);

        Delivery = new ReportDeliveryService(
            Access,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            fixture.Clock,
            NullLogger<ReportDeliveryService>.Instance);

        Worker = new ReportRetryWorker(Delivery, options, fixture.Clock, NullLogger<ReportRetryWorker>.Instance);
    }

    public void Dispose()
    {
        Worker.Dispose();
        _provider.Dispose();
        Access.Dispose();
        Context.Dispose();
    }
}

/// <summary>
/// Позволяет дождаться регистрации фонового таймера
/// до продвижения виртуального времени.
/// </summary>
public sealed class RetryTimeProvider : FakeTimeProvider
{
    private readonly System.Threading.Channels.Channel<bool> _timers =
        global::System.Threading.Channels.Channel.CreateUnbounded<bool>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);

        _timers.Writer.TryWrite(true);

        return timer;
    }

    public async Task WaitForTimerAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await _timers.Reader.ReadAsync(timeout.Token);
    }
}
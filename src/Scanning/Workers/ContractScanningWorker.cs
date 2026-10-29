using ContractWatcher.SDK.Scanning.Exceptions;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractWatcher.SDK.Scanning.Workers;

/// <summary>
/// Один раз запускает сканирование после события ApplicationStarted.
/// </summary>
/// <remarks>
/// <para>
/// Ожидание старта и retry не блокируют запуск host.
/// Ошибка сканирования логируется, но не останавливает приложение.
/// </para>
/// <para>
/// Контекст и прогресс существуют только в памяти текущего запуска.
/// После перезапуска приложения выполняется новый проход.
/// </para>
/// </remarks>
internal sealed class ContractScanningWorker(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<ContractScanningWorker> logger,
    ScanningCompletion? completion = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());

            await started.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            await using var scope = scopeFactory.CreateAsyncScope();

            var scanner = scope.ServiceProvider.GetRequiredService<IContractScanner>();

            var contracts = await scanner
                .ScanAsync(cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Contract scanning completed. Generated {Count} contracts.", contracts.Count);
            completion?.Complete(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Штатная остановка приложения
        }
        catch (ScanningException exception)
        {
            logger.LogError(exception,
                "Contract scanning stopped at {Stage}, module {Module}, item {Item}, after {Attempts} attempts.",
                exception.Stage, exception.ModuleType.Name, exception.Item, exception.Attempts);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not complete contract scanning.");
        }
        finally
        {
            // TrySetResult внутри Complete не перезапишет успешный результат.
            // Если сканирование упало или отменилось, воркер не начнёт
            // отправлять очередь до проверки актуальных правил приложения.
            completion?.Complete(false);
        }
    }
}
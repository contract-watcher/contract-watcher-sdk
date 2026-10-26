using ContractWatcher.SDK.Reporting.Options;
using ContractWatcher.SDK.Reporting.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Reporting.Workers;

/// <summary>
/// Повторно отправляет ожидающие отчёты,
/// в том числе восстановленные после перезапуска.
/// </summary>
internal sealed class ReportRetryWorker(
    ReportDeliveryService delivery,
    IOptions<ReportRetryOptions> options,
    TimeProvider clock,
    ILogger<ReportRetryWorker> logger) : BackgroundService
{
    /// <summary>
    /// Обрабатывает очередь сразу после запуска и далее с заданным интервалом.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not read or persist the report retry queue.");
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, clock, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Обрабатывает одну партию.
    /// Ошибка отдельного отчёта не блокирует остальные.
    /// </summary>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var ids = await delivery.GetDueAsync(cancellationToken).ConfigureAwait(false);

        foreach (var id in ids)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                await delivery.TrySendAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not deliver report {ReportId}.", id);
            }
        }

        await delivery.CleanupAsync(cancellationToken) .ConfigureAwait(false);
    }
}
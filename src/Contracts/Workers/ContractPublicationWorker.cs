using ContractWatcher.SDK.Contracts.Options;
using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Scanning.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Contracts.Workers;

/// <summary>
/// Выполняет сохранённые операции доставки контрактов.
/// </summary>
/// <remarks>
/// Ожидает успешного startup scan.
/// Не выполняет повторное сканирование приложения.
/// </remarks>
internal sealed class ContractPublicationWorker(
    ContractDeliveryService delivery,
    ScanningCompletion completion,
    IOptions<ContractPublicationOptions> options,
    TimeProvider clock,
    ILogger<ContractPublicationWorker> logger) : BackgroundService
{
    /// <summary>
    /// Ожидает startup scan и периодически обрабатывает очередь.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!await completion.WaitAsync(stoppingToken).ConfigureAwait(false))
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await delivery.RunOnceAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not process the contract publication queue.");
                }

                await Task.Delay(options.Value.PollInterval, clock, stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка во время ожидания startup scan или таймера.
        }
    }
}
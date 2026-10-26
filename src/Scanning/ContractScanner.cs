using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Scanning.Exceptions;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Scanning;

/// <summary>
/// Последовательно выполняет модули в порядке регистрации.
/// </summary>
/// <remarks>
/// <para>
/// При ошибке повторяет только текущий модуль с прежним контекстом.
/// Успешные предыдущие этапы повторно не выполняются.
/// </para>
/// <para>
/// После исчерпания попыток следующие этапы не запускаются.
/// Отмена вызывающего кода прекращает выполнение без повторов.
/// </para>
/// </remarks>
internal sealed class ContractScanner(
    IEnumerable<IScanningModule> modules,
    IOptions<ScanningRetryOptions> options,
    TimeProvider clock,
    ILogger<ContractScanner> logger) : IContractScanner
{
    public async Task<IReadOnlyCollection<PublishedContract>> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var context = new ScanningContext();
        var maxAttempts = options.Value.MaxAttempts;
        var delay = options.Value.Delay;

        foreach (var module in modules)
        {
            var stage = module.Stage;

            for (var attempt = 1; ; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                context.CurrentItem = null;

                try
                {
                    await module.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException();

                    break;
                }
                catch (Exception exception)when (!cancellationToken.IsCancellationRequested)
                {
                    if (attempt >= maxAttempts)
                        throw new ScanningException(stage, module.GetType(), attempt, context.CurrentItem, exception);

                    logger.LogWarning(exception,
                        "Scanning stage {Stage}, module {Module}, item {Item}: attempt {Attempt}/{MaxAttempts} failed. Retrying after {Delay}.",
                        stage, module.GetType().Name, context.CurrentItem, attempt, maxAttempts, delay);

                    await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return context.Contracts ?? throw new InvalidOperationException(
            "Scanning pipeline completed without producing contracts.");
    }
}
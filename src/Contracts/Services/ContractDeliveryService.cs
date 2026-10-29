using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Contracts.Services;

/// <summary>
/// Управляет обработкой очереди доставки контрактов.
/// </summary>
/// <remarks>
/// Отвечает за проход очереди, scope одной попытки,
/// общую блокировку и обработку ошибок.
/// </remarks>
internal sealed class ContractDeliveryService(
    ContractDeliveryStore deliveryStore,
    IServiceScopeFactory scopes,
    ContractDeliveryGate gate,
    IOptions<ContractSynchronizationOptions> synchronization,
    ILogger<ContractDeliveryService> logger)
{
    /// <summary>
    /// Обрабатывает готовую партию и очищает завершённые операции.
    /// </summary>
    /// <remarks>
    /// Ошибка отдельного контракта не останавливает остальные операции.
    /// Отмена приложения распространяется вызывающему коду.
    /// </remarks>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (synchronization.Value.SourcePreference != ContractSourcePreference.Application)
            return;

        var slugs = await deliveryStore
            .GetDueSlugsAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var slug in slugs)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                await TrySendAsync(slug, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception, "Could not persist delivery progress for contract {Slug}.", slug);
            }
        }

        await deliveryStore.CleanupAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Выполняет одну попытку доставки под общей блокировкой.
    /// </summary>
    /// <remarks>
    /// Блокировка также используется модулем сохранения результатов сканирования,
    /// поэтому правила текущей редакции не заменяются во время отправки.
    /// </remarks>
    internal async Task TrySendAsync(string slug, CancellationToken cancellationToken)
    {
        if (synchronization.Value.SourcePreference != ContractSourcePreference.Application)
            return;

        await gate.Semaphore.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var context = await deliveryStore
                .PrepareAsync(slug, cancellationToken)
                .ConfigureAwait(false);

            if (context is null)
                return;

            await using var scope = scopes.CreateAsyncScope();

            try
            {
                var processor = scope.ServiceProvider.GetRequiredService<ContractDeliveryProcessor>();

                await processor.ExecuteAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                await deliveryStore.FailAsync(context, exception, cancellationToken)
                    .ConfigureAwait(false);

                logger.LogWarning(
                    exception, "Contract {Slug} failed at stage {Stage}.", slug, context.Stage);
            }
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }
}
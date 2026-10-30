using ContractWatcher.SDK.Contracts.Comparers;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Contracts.Services;

/// <summary>
/// Выполняет этапы доставки одного контракта.
/// </summary>
/// <remarks>
/// Не выбирает операции из очереди, не управляет блокировками
/// и не назначает повторные попытки.
/// Ошибки передаются вызывающему сервису.
/// </remarks>
internal sealed class ContractDeliveryProcessor(
    IContractWatcherCoreClient client,
    IContractStore contracts,
    ContractDeliveryStore deliveryStore)
{
    /// <summary>
    /// Продолжает доставку с текущего этапа.
    /// </summary>
    /// <remarks>
    /// Этап контекста изменяется только после успешного сохранения перехода.
    /// </remarks>
    internal async Task ExecuteAsync(ContractDeliveryContext context, CancellationToken cancellationToken)
    {
        if (context.Stage is ScanningStage.Synchronization or ScanningStage.Publication)
            await PublishAsync(context, cancellationToken)
                .ConfigureAwait(false);

        if (context.Stage != ScanningStage.Persistence)
            throw new InvalidOperationException($"Unsupported delivery stage: {context.Stage}.");

        var confirmed = context.ToContract();
        var local = contracts.Get(context.Slug);

        // Refresher мог уже получить более новую опубликованную версию.
        if (local is null || local.ContractVersion <= confirmed.ContractVersion)
            await contracts.SetAsync(confirmed, cancellationToken)
                .ConfigureAwait(false);

        await deliveryStore.CompleteAsync(context.Slug, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Сверяет состояние Core, назначает версию
    /// и при необходимости публикует контракт.
    /// </summary>
    /// <remarks>
    /// Сверка выполняется и перед повторным POST:
    /// предыдущий запрос мог сохраниться в Core,
    /// даже если SDK не получил подтверждение.
    /// </remarks>
    private async Task PublishAsync(ContractDeliveryContext context, CancellationToken cancellationToken)
    {
        var current = await client
            .GetContractAsync(context.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (current is not null &&
            (!StringComparer.Ordinal.Equals(current.Slug, context.Slug) || current.ContractVersion <= 0))
            throw new InvalidOperationException("Core returned an incompatible contract.");

        var equal = current is not null && 
            ContractContentComparer.AreEqual(context.Slug, context.Rules, current);

        context.Version = equal ? 
            current!.ContractVersion : checked((current?.ContractVersion ?? 0) + 1);

        var next = equal ? ScanningStage.Persistence : ScanningStage.Publication;

        await deliveryStore.SaveVersionAsync(
                context, current?.ContractVersion ?? 0, next, cancellationToken)
            .ConfigureAwait(false);

        context.Stage = next;

        if (!equal)
        {
            var published = await client
                .PublishContractAsync(context.ToContract(), cancellationToken)
                .ConfigureAwait(false);

            // Core назначает окончательную версию.
            // Проверяем, что подтверждены именно отправленные правила.
            if (!ContractContentComparer.AreEqual(context.Slug, context.Rules, published))
                throw new InvalidOperationException("Core returned different rules after publication.");

            context.Version = published.ContractVersion;

            // Сохраняем окончательную версию вместе с переходом к Persistence.
            // После перезапуска локальное подтверждение использует версию Core.
            await deliveryStore.ConfirmPublicationAsync(
                context.Slug, published.ContractVersion, cancellationToken)
                .ConfigureAwait(false);

            context.Stage = ScanningStage.Persistence;
        }
    }
}
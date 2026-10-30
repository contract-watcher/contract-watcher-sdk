using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Запускает первоначальную доставку сохранённых контрактов.
/// </summary>
/// <remarks>
/// Ошибки отдельных отправок сохраняются в очереди.
/// Успешное завершение модуля не означает, что Core уже подтвердил все контракты.
/// </remarks>
internal sealed class ContractPublicationModule(ContractDeliveryService delivery) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Publication;

    public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (context.MappingResults is null || !context.PersistenceCompleted)
            throw new InvalidOperationException("Mapping and persistence must precede publication.");

        return delivery.RunOnceAsync(cancellationToken);
    }
}
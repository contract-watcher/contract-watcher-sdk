using System.Text.Json;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Watcher.Interfaces;
using ContractWatcher.SDK.Watcher.Models;

namespace ContractWatcher.SDK.Watcher;

/// <summary>
/// Выполняет явную проверку входящего JSON через основной execution pipeline SDK
/// </summary>
public sealed class ContractWatcher : IContractWatcher
{
    /// <summary>
    /// Execution pipeline проверки контракта
    /// </summary>
    private readonly IExecutionPipeline _executionPipeline;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ContractWatcher"/>
    /// </summary>
    /// <param name="executionPipeline">Pipeline выполнения проверки контракта</param>
    public ContractWatcher(IExecutionPipeline executionPipeline) =>
        _executionPipeline = executionPipeline ?? throw new ArgumentNullException(nameof(executionPipeline));
    
    public async Task<ContractWatchResult> ValidateAsync(string contractSlug, JsonElement payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractSlug))
            throw new ArgumentException(nameof(contractSlug));

        var context = new WatcherContext
        {
            ContractSlug = contractSlug,
            Payload = payload
        };

        await _executionPipeline.ExecuteAsync(context, cancellationToken);

        return new ContractWatchResult
        {
            Status = context.Status,
            Violations = context.ValidationResult?.Violations ?? []
        };
    }
}
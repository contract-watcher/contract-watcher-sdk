using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Получает опубликованные контракты
/// до генерации схем и назначения версий.
/// </summary>
/// <remarks>
/// Использует slug из discovery.
/// Ошибка Core сохраняет возможность работы с локальной копией.
/// Результат этапа записывается в контекст целиком.
/// </remarks>
public sealed class ContractSynchronizationModule(
    IContractStore store,
    IContractRefresher refresher,
    IOptions<ContractSynchronizationOptions> options,
    ILogger<ContractSynchronizationModule> logger) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Synchronization;

    public async Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var endpoints = context.Endpoints
            ?? throw new InvalidOperationException("Endpoint discovery has not completed.");

        var preference = options.Value.SourcePreference;

        if (!Enum.IsDefined(preference))
            throw new InvalidOperationException("Unknown contract source preference.");

        var slugs = endpoints
            .Select(endpoint => endpoint.Slug)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var cached = new Dictionary<string, PublishedContract>(StringComparer.Ordinal);

        foreach (var slug in slugs)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            context.CurrentItem = slug;

            var contract = store.Get(slug);

            if (contract is not null)
                cached.Add(slug, contract);
        }

        var results = await refresher
            .RefreshManyAsync(slugs, cancellationToken)
            .ConfigureAwait(false);

        var bySlug = results.ToDictionary(result => 
            result.Slug, StringComparer.Ordinal);

        if (bySlug.Count != slugs.Length || slugs.Any(slug => !bySlug.ContainsKey(slug)))
            throw new InvalidOperationException("Refresher must return one result for each requested slug.");

        foreach (var result in results.Where(result => result.Error is not null))
        {
            logger.LogWarning(
                result.Error, "Could not refresh contract '{Slug}'. Using local fallback.", result.Slug);
        }
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        context.SourcePreference = preference;
        context.CachedContracts = cached;
        context.RefreshedContracts = results;
    }
}
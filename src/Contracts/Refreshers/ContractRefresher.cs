using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Core.Interfaces;

namespace ContractWatcher.SDK.Contracts.Refreshers;

/// <summary>
/// Загружает актуальные контракты из Core и обновляет локальное хранилище.
/// </summary>
internal sealed class ContractRefresher(
    IContractStore contractStore,
    IContractWatcherCoreClient coreClient) : IContractRefresher
{
    /// <inheritdoc />
    public async Task<PublishedContract?> RefreshAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var contract = await coreClient
            .GetContractAsync(slug, cancellationToken)
            .ConfigureAwait(false);

        if (contract is null)
        {
            await contractStore
                .RemoveAsync(slug, cancellationToken)
                .ConfigureAwait(false);

            return null;
        }

        await contractStore
            .SetAsync(contract, cancellationToken)
            .ConfigureAwait(false);

        return contract;
    }

    /// <summary>
    /// Получает контракты из Core и пакетом
    /// сохраняет успешные результаты.
    /// </summary>
    /// <remarks>
    /// Ошибки обращения к Core не стирают локальные данные
    /// и не прерывают остальные запросы.
    /// Ошибки записи базы и ошибки программирования
    /// передаются вызывающему коду.
    /// </remarks>
    public async Task<IReadOnlyCollection<ContractRefreshResult>> RefreshManyAsync(
        IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken = default)
    {
        if (slugs is null)
            throw new ArgumentNullException(nameof(slugs));

        var keys = slugs
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (keys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Contract slug is required.", nameof(slugs));

        var results = new List<ContractRefreshResult>();

        foreach (var slug in keys)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                var contract = await coreClient
                    .GetContractAsync(slug, cancellationToken)
                    .ConfigureAwait(false);

                if (contract is not null &&
                    (!StringComparer.Ordinal.Equals(slug, contract.Slug) || contract.ContractVersion <= 0))
                    throw new InvalidOperationException("Core returned an incompatible contract.");

                results.Add(new ContractRefreshResult
                {
                    Slug = slug,
                    Contract = contract
                });
            }
            catch (CoreRequestException exception)when (!cancellationToken.IsCancellationRequested)
            {
                results.Add(new ContractRefreshResult
                {
                    Slug = slug,
                    Error = exception
                });
            }
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var received = results
            .Where(result => result.Error is null && result.Contract is not null)
            .Select(result => result.Contract!)
            .ToArray();

        var missing = results
            .Where(result => result.Error is null && result.Contract is null)
            .Select(result => result.Slug)
            .ToArray();

        await contractStore
            .ApplyAsync(received, missing, cancellationToken)
            .ConfigureAwait(false);

        return results;
    }
}
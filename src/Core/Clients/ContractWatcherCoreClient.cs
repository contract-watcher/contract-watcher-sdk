using System.Net;
using System.Net.Http.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Core.Constants;
using ContractWatcher.SDK.Core.Interfaces;

namespace ContractWatcher.SDK.Core.Clients;

/// <summary>
/// Выполняет HTTP-запросы к ContractWatcher Core
/// </summary>
public class ContractWatcherCoreClient(HttpClient httpClient) : IContractWatcherCoreClient
{
    public async Task<PublishedContract?> GetContractAsync(string slug, CancellationToken cancellationToken = default)
    {
        if  (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        using var response = await httpClient.GetAsync(
            $"{CoreRoutes.Contracts}/{Uri.EscapeDataString(slug)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PublishedContract>(cancellationToken);
    }
    
    public async Task PublishReportAsync(ValidationReport report, CancellationToken cancellationToken = default)
    {
        if (report is null)
            throw new ArgumentNullException(nameof(report));

        using var response = await httpClient.PostAsJsonAsync(CoreRoutes.Reports, report, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
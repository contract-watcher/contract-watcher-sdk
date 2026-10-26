using System.Net;
using System.Net.Http.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Core.Constants;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Core.Interfaces;

namespace ContractWatcher.SDK.Core.Clients;

/// <summary>
/// Выполняет HTTP-запросы к Core
/// </summary>
public class ContractWatcherCoreClient(HttpClient httpClient) : IContractWatcherCoreClient
{
    public async Task<PublishedContract?> GetContractAsync(string slug, CancellationToken cancellationToken = default)
    {
        if  (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentNullException(nameof(slug));

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{CoreRoutes.Contracts}/{Uri.EscapeDataString(slug)}");
        using var response = await SendAsync(request, cancellationToken, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        return await response.Content.ReadFromJsonAsync<PublishedContract>(cancellationToken);
    }
    
    public async Task PublishReportAsync(ValidationReport report, CancellationToken cancellationToken = default)
    {
        if (report is null)
            throw new ArgumentNullException(nameof(report));

        using var request = new HttpRequestMessage(HttpMethod.Post, CoreRoutes.Reports);
        request.Content = JsonContent.Create(report);

        using var response = await SendAsync(request, cancellationToken);
    }
    
    private static CoreRequestException CreateException(HttpStatusCode statusCode)
    {
        var error = statusCode switch 
        {
            HttpStatusCode.Unauthorized => CoreRequestError.Unauthorized,
            HttpStatusCode.Forbidden => CoreRequestError.Forbidden,
            _ when (int)statusCode >= 500 => CoreRequestError.ServerError,
            _ => CoreRequestError.UnexpectedResponse
        };

        return new CoreRequestException(error, $"Core returned HTTP {(int)statusCode}.", statusCode);
    }
    
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken, params HttpStatusCode[] allowedStatusCodes)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || allowedStatusCodes.Contains(response.StatusCode))
                return response;

            var exception = CreateException(response.StatusCode);
            response.Dispose();

            throw exception;
        }
        catch (OperationCanceledException exception)when (!cancellationToken.IsCancellationRequested)
        {
            throw new CoreRequestException(
                CoreRequestError.Timeout, 
                "The request to Core timed out.", 
                HttpStatusCode.RequestTimeout, 
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CoreRequestException(
                CoreRequestError.NetworkError,
                "A network error occurred while communicating with Core.",
                exception.StatusCode,
                exception);
        }
    }
}
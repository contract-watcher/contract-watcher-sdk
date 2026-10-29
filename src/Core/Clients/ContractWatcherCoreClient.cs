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

        return await response.Content.ReadFromJsonAsync<PublishedContract>(cancellationToken)
            ?? throw new CoreRequestException(CoreRequestError.UnexpectedResponse, "Core returned null instead of a contract.");
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
    
    /// <summary>
    /// Публикует контракт с версией, назначенной SDK.
    /// </summary>
    /// <param name="contract">Подготовленный контракт.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <remarks>
    /// Успешное завершение означает, что Core подтвердил сохранение.
    /// </remarks>
    public async Task PublishContractAsync(PublishedContract contract, CancellationToken cancellationToken = default)
    {
        if (contract is null)
            throw new ArgumentNullException(nameof(contract));

        if (string.IsNullOrWhiteSpace(contract.Slug))
            throw new ArgumentException("Contract slug is required.", nameof(contract));

        if (contract.ContractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contract), "Contract version must be positive.");

        using var request = new HttpRequestMessage
            (HttpMethod.Post, $"{CoreRoutes.PublishContract}/{Uri.EscapeDataString(contract.Slug)}") 
            { Content = JsonContent.Create(contract) };

        using var response = await SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Accepted означает принятие в обработку, но ещё не сохранение.
        // Для перехода к локальному подтверждению нужен окончательный успех.
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            throw new CoreRequestException(
                CoreRequestError.UnexpectedResponse,
                "Publication requires a durable acknowledgement; HTTP 202 is not sufficient.",
                response.StatusCode);
        }
    }
}
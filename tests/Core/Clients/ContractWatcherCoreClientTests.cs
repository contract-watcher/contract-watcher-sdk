using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContractWatcher.SDK.Core.Clients;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using Moq;
using Moq.Protected;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;
using static ContractWatcher.SDK.Tests.Fixtures.HttpFixture;
using static ContractWatcher.SDK.Tests.Fixtures.ConfigurationFixture;

namespace ContractWatcher.SDK.Tests.Core.Clients;

public sealed class ContractWatcherCoreClientTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetContractAsync_WhenSlugIsNullOrWhiteSpace_ThrowsArgumentException(string? slug)
    {
        // Arrange
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync(slug!);

        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task GetContractAsync_WhenContractExists_ReturnsContract()
    {
        // Arrange
        var contract = CreateContract(slug: "products", version: 3);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(contract) };
        using var httpClient = CreateHttpClient(response);
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var result = await client.GetContractAsync("products");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contract.Slug, result.Slug);
        Assert.Equal(contract.ContractVersion, result.ContractVersion);
    }

    [Fact]
    public async Task GetContractAsync_WhenContractDoesNotExist_ReturnsNull()
    {
        // Arrange
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var result = await client.GetContractAsync("products");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CoreRequestError.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CoreRequestError.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, CoreRequestError.ServerError)]
    [InlineData(HttpStatusCode.BadGateway, CoreRequestError.ServerError)]
    [InlineData(HttpStatusCode.BadRequest, CoreRequestError.UnexpectedResponse)]
    internal async Task GetContractAsync_WhenCoreReturnsError_ThrowsExpectedCoreRequestException(HttpStatusCode statusCode, CoreRequestError expectedError)
    {
        // Arrange
        using var httpClient = CreateHttpClient(new HttpResponseMessage(statusCode));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync("products");

        // Assert
        var exception = await Assert.ThrowsAsync<CoreRequestException>(action);

        Assert.Equal(expectedError, exception.Error);
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task GetContractAsync_SendsGetRequestForContract()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        
        var contract = CreateContract();
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => { capturedRequest = request; })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(contract) });

        using var httpClient = new HttpClient(handler.Object);
        httpClient.BaseAddress = new Uri(CoreUrl);
        
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        await client.GetContractAsync("products");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.EndsWith("/api/sdk/contracts/products", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task PublishReportAsync_WhenReportIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.PublishReportAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Fact]
    public async Task PublishReportAsync_WhenCoreAcceptsReport_CompletesSuccessfully()
    {
        // Arrange
        var report = CreateReport();
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        await client.PublishReportAsync(report);

        // Assert
        // Отсутствие исключения означает успешную публикацию
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CoreRequestError.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CoreRequestError.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, CoreRequestError.ServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CoreRequestError.ServerError)]
    [InlineData(HttpStatusCode.NotFound, CoreRequestError.UnexpectedResponse)]
    internal async Task PublishReportAsync_WhenCoreReturnsError_ThrowsExpectedCoreRequestException(HttpStatusCode statusCode, CoreRequestError expectedError)
    {
        // Arrange
        var report = CreateReport();
        using var httpClient = CreateHttpClient(new HttpResponseMessage(statusCode));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.PublishReportAsync(report);

        // Assert
        var exception = await Assert.ThrowsAsync<CoreRequestException>(action);
        Assert.Equal(expectedError, exception.Error);
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task PublishReportAsync_SendsPostRequestWithReport()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var report = CreateReport();
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(
                (request, _) =>
                {
                    capturedRequest = request;
                    capturedBody = request.Content!
                        .ReadAsStringAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(handler.Object);
        httpClient.BaseAddress = new Uri(CoreUrl);
        
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        await client.PublishReportAsync(report);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.EndsWith("/api/sdk/reports", capturedRequest.RequestUri!.AbsolutePath);

        using var document = JsonDocument.Parse(capturedBody);

        Assert.Equal(report.ReportId, document.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(report.ContractVersion, document.RootElement.GetProperty("contractVersion").GetInt32());
    }
    
    [Fact]
    public async Task GetContractAsync_WhenNetworkErrorOccurs_ThrowsNetworkError()
    {
        // Arrange
        var networkException = new HttpRequestException("Network failure");

        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(networkException);

        using var httpClient = new HttpClient(handler.Object);
        httpClient.BaseAddress = new Uri(CoreUrl);

        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync("products");

        // Assert
        var exception = await Assert.ThrowsAsync<CoreRequestException>(action);
        Assert.Equal(CoreRequestError.NetworkError, exception.Error);
        Assert.Same(networkException, exception.InnerException);
    }
    
    [Fact]
    public async Task GetContractAsync_WhenRequestTimesOut_ThrowsTimeoutError()
    {
        // Arrange
        var timeoutException = new TaskCanceledException();
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(timeoutException);

        using var httpClient = new HttpClient(handler.Object);
        httpClient.BaseAddress = new Uri(CoreUrl);

        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync("products");

        // Assert
        var exception = await Assert.ThrowsAsync<CoreRequestException>(action);
        Assert.Equal(CoreRequestError.Timeout, exception.Error);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
        Assert.Same(timeoutException, exception.InnerException);
    }
    
    [Fact]
    public async Task GetContractAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var cancellationToken = cancellationTokenSource.Token;
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage _, CancellationToken token) => Task.FromCanceled<HttpResponseMessage>(token));

        using var httpClient = new HttpClient(handler.Object);
        httpClient.BaseAddress = new Uri(CoreUrl);

        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync("products", cancellationToken);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }
}
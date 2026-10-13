using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContractWatcher.SDK.Core.Clients;
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

    [Fact]
    public async Task GetContractAsync_WhenCoreReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.GetContractAsync("products");

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(action);
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

        using var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri(CoreUrl) };
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        await client.GetContractAsync("products");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.EndsWith("/contracts/products", capturedRequest.RequestUri!.AbsolutePath);
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

    [Fact]
    public async Task PublishReportAsync_WhenCoreReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        var report = CreateReport();
        using var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        var action = () => client.PublishReportAsync(report);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(action);
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

        using var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri(CoreUrl) };
        var client = new ContractWatcherCoreClient(httpClient);

        // Act
        await client.PublishReportAsync(report);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.EndsWith("/reports", capturedRequest.RequestUri!.AbsolutePath);

        using var document = JsonDocument.Parse(capturedBody);

        Assert.Equal(report.ReportId, document.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(report.ContractVersion, document.RootElement.GetProperty("contractVersion").GetInt32());
    }
}
using Moq;
using Moq.Protected;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ConfigurationFixture;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class HttpFixture
{
    public static HttpClient CreateHttpClient(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();

        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(handler.Object)
        {
            BaseAddress = new Uri(CoreUrl)
        };
    }
}
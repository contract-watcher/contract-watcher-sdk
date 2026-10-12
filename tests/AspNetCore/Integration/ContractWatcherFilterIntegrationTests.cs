using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContractWatcher.SDK.Execution.Enums;
using ContractWatcher.SDK.Watcher.Interfaces;
using ContractWatcher.SDK.Watcher.Models;
using ContractWatcher.SDK.Tests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.AspNetCoreIntegrationFixture;

namespace ContractWatcher.SDK.Tests.AspNetCore.Integration;

public sealed class ContractWatcherFilterIntegrationTests
{
    [Fact]
    public async Task WatchedEndpoint_WhenRequestIsValid_CallsWatcherAndPreservesModelBinding()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        string? capturedSlug = null;
        
        JsonElement capturedPayload = default;

        contractWatcher.Setup(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, JsonElement, CancellationToken>(
                (slug, payload, _) =>
                {
                    capturedSlug = slug;
                    capturedPayload = payload.Clone();
                })
            .ReturnsAsync(new ContractWatchResult
                {
                    Status = WatcherStatus.Passed,
                    Violations = []
                });

        using var host = await CreateHostAsync(contractWatcher.Object);
        var client = host.GetTestClient();
        var request = new TestRequest("RTX 5090", 1999m);

        // Act
        var response = await client.PostAsJsonAsync("/integration/watched", request);
        var responseBody = await response.Content.ReadFromJsonAsync<TestRequest>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(responseBody);
        Assert.Equal(request, responseBody);
        Assert.Equal("products", capturedSlug);
        Assert.Equal("RTX 5090", capturedPayload.GetProperty("name").GetString());
        Assert.Equal(1999m, capturedPayload.GetProperty("price").GetDecimal());

        contractWatcher.Verify(x => x.ValidateAsync(
                "products",
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlainEndpoint_WhenRequestIsSent_DoesNotCallWatcher()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();
        using var host = await CreateHostAsync(contractWatcher.Object);
        var client = host.GetTestClient();
        var request = new TestRequest("RTX 5090", 1999m);

        // Act
        var response = await client.PostAsJsonAsync("/integration/plain", request);
        var responseBody = await response.Content.ReadFromJsonAsync<TestRequest>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(request, responseBody);

        contractWatcher.Verify(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WatchedEndpoint_WhenValidationFails_DoesNotChangeEndpointResponse()
    {
        // Arrange
        var contractWatcher = new Mock<IContractWatcher>();

        contractWatcher.Setup(x => x.ValidateAsync(
                "products",
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContractWatchResult
                {
                    Status = WatcherStatus.Failed,
                    Violations = []
                });

        using var host = await CreateHostAsync(contractWatcher.Object);
        var client = host.GetTestClient();
        var request = new TestRequest("RTX 5090", 1999m);

        // Act
        var response = await client.PostAsJsonAsync("/integration/watched", request);
        var responseBody = await response.Content.ReadFromJsonAsync<TestRequest>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(request, responseBody);

        contractWatcher.Verify(x => x.ValidateAsync(
                "products",
                It.IsAny<JsonElement>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
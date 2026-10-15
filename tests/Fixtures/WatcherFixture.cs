using System.Text.Json;
using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class WatcherFixture
{
    public static WatcherContext CreateContext()
    {
        using var document = JsonDocument.Parse("{}");

        return new WatcherContext
        {
            ContractSlug = "test-contract",
            Payload = document.RootElement.Clone()
        };
    }
}
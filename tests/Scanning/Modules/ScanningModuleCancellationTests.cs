using System.Collections;
using System.Text.Json.Nodes;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class ScanningModuleCancellationTests
{
    [Theory]
    [InlineData("discovery")]
    [InlineData("schema")]
    [InlineData("mapping")]
    public async Task ExecuteAsync_WhenContextIsNull_Throws(string stage)
    {
        // Arrange
        IScanningModule module = stage switch
        {
            "discovery" => Discovery(),
            "schema" => SchemaModule(),
            _ => new ContractMappingModule()
        };

        // Act
        var action = () => module.ExecuteAsync(null!, default);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
    }

    [Theory]
    [InlineData("discovery", "before")]
    [InlineData("discovery", "during")]
    [InlineData("discovery", "after")]
    [InlineData("schema", "before")]
    [InlineData("schema", "during")]
    [InlineData("schema", "after")]
    [InlineData("mapping", "before")]
    [InlineData("mapping", "during")]
    [InlineData("mapping", "after")]
    public async Task ExecuteAsync_WhenCanceled_DoesNotCommitResult(string stage, string moment)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();

        if (moment == "before")
            cancellation.Cancel();

        var endpoints = new CancelingList<DiscoveredEndpoint>([Endpoint()], cancellation, moment);

        var context = new ScanningContext
        {
            Endpoints = endpoints,
            Schemas = new Dictionary<Type, JsonNode>
            {
                [typeof(Request)] = JsonNode.Parse("""{"type":"object"}""")!
            }
        };

        IScanningModule module;

        if (stage == "discovery")
        {
            var actions = new CancelingList<ActionDescriptor>(
                [
                    ActionDescriptor(nameof(ScanController.Explicit), BindingSource.Body)
                ],
                cancellation,
                moment);

            var provider = new Mock<IActionDescriptorCollectionProvider>();

            provider.SetupGet(value => value.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actions, 0));

            module = new EndpointDiscoveryModule(provider.Object);
            context.Endpoints = null;
        }
        else if (stage == "schema")
        {
            module = SchemaModule();
            context.Schemas = null;
        }
        else
        {
            module = new ContractMappingModule();
        }

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => module.ExecuteAsync(context, cancellation.Token));

        // Assert
        if (stage == "discovery")
            Assert.Null(context.Endpoints);

        if (stage == "schema")
            Assert.Null(context.Schemas);

        Assert.Null(context.Contracts);
    }

    private sealed class CancelingList<T>(
        IReadOnlyList<T> items,
        CancellationTokenSource cancellation,
        string moment) : IReadOnlyList<T>
    {
        public int Count => items.Count;

        public T this[int index] => items[index];

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var item in items)
            {
                if (moment == "during")
                    cancellation.Cancel();

                yield return item;
            }

            if (moment == "after") 
                cancellation.Cancel();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
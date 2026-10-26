using System.Text.Json.Nodes;
using ContractWatcher.SDK.Scanning.Models;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class SchemaGenerationModuleTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDtosRepeat_ProducesOneSchemaWithJsonMetadata()
    {
        // Arrange
        var module = SchemaModule();
        var context = Context(Endpoint("create"), Endpoint("update"));

        // Act
        await module.ExecuteAsync(context, default);

        // Assert
        var schema = Assert.Single(context.Schemas!).Value;
        var properties = schema["properties"]!.AsObject();

        Assert.Equal(5, properties.Count);
        Assert.Contains(
            "display_name", properties.Select(property => property.Key));

        Assert.False(properties.ContainsKey("secret"));

        Assert.Contains("display_name", schema["required"]!
            .AsArray()
            .Select(value => value!.GetValue<string>()));

        Assert.Equal("number", properties["price"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLaterTypeFails_DoesNotCommitPartialSchemas()
    {
        // Arrange
        var module = SchemaModule();

        var context = Context(
            Endpoint(),
            Endpoint("unsupported", typeof(ConflictingRequest)));

        var previous = new Dictionary<Type, JsonNode>();
        context.Schemas = previous;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => module.ExecuteAsync(context, default));

        // Assert
        Assert.Same(previous, context.Schemas);
        Assert.Empty(context.Schemas!);
        Assert.Equal(typeof(ConflictingRequest).FullName, context.CurrentItem);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscoveryHasNotCompleted_Throws()
    {
        // Arrange
        var module = SchemaModule();
        var context = new ScanningContext();

        // Act
        var action = () => module.ExecuteAsync(context, default);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Null(context.Schemas);
    }
}
using System.Text.Json.Nodes;
using ContractWatcher.Common.Enums;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class ContractMappingModuleTests
{
    [Theory]
    [InlineData("string", ContractFieldType.String)]
    [InlineData("integer", ContractFieldType.Number)]
    [InlineData("number", ContractFieldType.Number)]
    [InlineData("boolean", ContractFieldType.Boolean)]
    [InlineData("object", ContractFieldType.Object)]
    [InlineData("array", ContractFieldType.Array)]
    public async Task ExecuteAsync_WhenTypeIsSupported_MapsRule(string type, ContractFieldType expected)
    {
        // Arrange
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["value"] = new JsonObject { ["type"] = type }
            }
        };

        var context = Context(Endpoint());

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = schema
        };

        // Act
        await new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        var contract = Assert.Single(context.Contracts!);

        Assert.Equal("products", contract.Slug);

        var rule = Assert.Single(contract.Rules);

        Assert.Equal("value", rule.FieldName);
        Assert.Equal(expected, rule.Type);
        Assert.False(rule.Required);
        Assert.False(rule.Nullable);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExecuteAsync_WhenFlagsDiffer_PreservesRequiredAndNullable(bool required, bool nullable)
    {
        // Arrange
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["value"] = new JsonObject
                {
                    ["type"] = nullable
                        ? new JsonArray("object", "null")
                        : JsonValue.Create("object"),

                    ["properties"] = new JsonObject
                    {
                        ["nested"] = new JsonObject
                        {
                            ["type"] = "string"
                        }
                    }
                }
            },
            ["required"] = required ? new JsonArray("value") : new JsonArray()
        };

        var context = Context(Endpoint());

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = schema
        };

        // Act
        await new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        var rule = Assert.Single(Assert.Single(context.Contracts!).Rules);

        Assert.Equal("value", rule.FieldName);
        Assert.Equal(ContractFieldType.Object, rule.Type);
        Assert.Equal(required, rule.Required);
        Assert.Equal(nullable, rule.Nullable);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLaterSchemaFails_DoesNotCommitPartialContracts()
    {
        // Arrange
        var context = Context(
            Endpoint("valid"), Endpoint("invalid", typeof(Details)));

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = JsonNode.Parse("""{"type":"object","properties":{}}""")!,
            [typeof(Details)] = JsonNode.Parse("""{"type":"array"}""")!
        };

        // Act
        await Assert.ThrowsAsync<NotSupportedException>(() => new ContractMappingModule()
            .ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Contracts);
        Assert.Equal("invalid", context.CurrentItem);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMvcAllowsNumbersAsStrings_RejectsUnrepresentableUnion()
    {
        // Arrange
        var context = Context(Endpoint());

        await SchemaModule(strictNumbers: false)
            .ExecuteAsync(context, default);

        // Act
        await Assert.ThrowsAsync<NotSupportedException>(() => new ContractMappingModule()
            .ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Contracts);
    }

    [Theory]
    [InlineData("endpoints")]
    [InlineData("schemas")]
    [InlineData("schema-for-type")]
    public async Task ExecuteAsync_WhenInputIsMissing_Throws(string missing)
    {
        // Arrange
        var context = missing == "endpoints" ? new ScanningContext() : Context(Endpoint());

        if (missing == "schema-for-type")
            context.Schemas = new Dictionary<Type, JsonNode>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ContractMappingModule()
            .ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Contracts);
    }
    
    [Theory]
    [InlineData("true")]
    [InlineData("""{"type":"object","anyOf":[]}""")]
    [InlineData("""{"type":"object","oneOf":[]}""")]
    [InlineData("""{"type":"object","allOf":[]}""")]
    [InlineData("""{"type":"object","not":{}}""")]
    [InlineData("""{"type":"object","properties":{"value":null}}""")]
    [InlineData("""{"type":"object","properties":{"value":{"type":"unknown"}}}""")]
    [InlineData("""{"type":"object","properties":{"value":{}}}""")]
    public async Task ExecuteAsync_WhenSchemaIsUnsupported_DoesNotProduceContracts(string json)
    {
        // Arrange
        var context = Context(Endpoint());

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = JsonNode.Parse(json)!
        };

        // Act
        await Assert.ThrowsAsync<NotSupportedException>(
            () => new ContractMappingModule().ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Contracts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenCustomDiscoveryReturnsInvalidSlug_Throws(string? slug)
    {
        // Arrange
        var context = Context(Endpoint(slug!));

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = JsonNode.Parse("""{"type":"object"}""")!
        };

        // Act
        var action = () => new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(action);
        Assert.Null(context.Contracts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomGeneratorReturnsNullSchema_Throws()
    {
        // Arrange
        var context = Context(Endpoint());

        context.Schemas = new Dictionary<Type, JsonNode>
        {
            [typeof(Request)] = null!
        };

        // Act
        var action = () => new ContractMappingModule().ExecuteAsync(context, default);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(action);
        Assert.Null(context.Contracts);
    }
}
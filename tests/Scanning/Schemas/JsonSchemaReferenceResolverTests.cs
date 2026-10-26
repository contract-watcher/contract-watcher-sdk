using System.Text.Json.Nodes;
using ContractWatcher.SDK.Scanning.Schemas;

namespace ContractWatcher.SDK.Tests.Scanning.Schemas;

public sealed class JsonSchemaReferenceResolverTests
{
    [Theory]
    [InlineData("#/$defs/name", "name")]
    [InlineData("#/$defs/a~1b", "a/b")]
    [InlineData("#/$defs/a~0b", "a~b")]
    [InlineData("#/$defs/a%20b", "a b")]
    public void Resolve_WhenPointerIsEscaped_ReturnsTarget(string pointer, string key)
    {
        // Arrange
        var target = new JsonObject { ["type"] = "string" };

        var root = new JsonObject
        {
            ["$defs"] = new JsonObject { [key] = target }
        };

        var reference = new JsonObject { ["$ref"] = pointer };

        // Act
        var result = JsonSchemaReferenceResolver.Resolve(reference, root);

        // Assert
        Assert.Same(target, result);
    }

    [Fact]
    public void Resolve_WhenReferencePointsToRoot_ReturnsRoot()
    {
        // Arrange
        var root = JsonNode.Parse("""{"type":"object"}""")!;
        var reference = JsonNode.Parse("""{"$ref":"#"}""")!;

        // Act
        var result = JsonSchemaReferenceResolver.Resolve(reference, root);

        // Assert
        Assert.Same(root, result);
    }

    [Theory]
    [InlineData("""{"$ref":"https://example.com/schema"}""")]
    [InlineData("""{"$ref":"#"}""")]
    [InlineData("""{"$ref":"#","type":"object"}""")]
    public void Resolve_WhenReferenceIsUnsupported_Throws(string json)
    {
        // Arrange
        var schema = JsonNode.Parse(json)!;

        // Act
        var action = () => JsonSchemaReferenceResolver.Resolve(schema, schema);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    [Fact]
    public void Resolve_WhenTargetDoesNotExist_Throws()
    {
        // Arrange
        var root = JsonNode.Parse("""{"$ref":"#/$defs/missing"}""")!;

        // Act
        var action = () => JsonSchemaReferenceResolver.Resolve(root, root);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }
    
    [Fact]
    public void Resolve_WhenArgumentsAreNull_Throws()
    {
        // Arrange
        var schema = JsonNode.Parse("{}")!;

        // Act
        var missingSchema = () =>
            JsonSchemaReferenceResolver.Resolve(null!, schema);

        var missingRoot = () =>
            JsonSchemaReferenceResolver.Resolve(schema, null!);

        // Assert
        Assert.Throws<ArgumentNullException>("schema", missingSchema);
        Assert.Throws<ArgumentNullException>("root", missingRoot);
    }

    [Fact]
    public void Resolve_WhenPointerContainsArrayIndex_ReturnsElement()
    {
        // Arrange
        var root = JsonNode.Parse("""{"definitions":[{"type":"string"}]}""")!;
        var reference = JsonNode.Parse("""{"$ref":"#/definitions/0"}""")!;

        // Act
        var result = JsonSchemaReferenceResolver.Resolve(reference, root);

        // Assert
        Assert.Same(root["definitions"]![0], result);
    }

    [Theory]
    [InlineData("#/definitions/no-index")]
    [InlineData("#/definitions/-1")]
    [InlineData("#/definitions/2")]
    [InlineData("#/definitions/0/type/child")]
    public void Resolve_WhenPointerCannotBeTraversed_Throws(string pointer)
    {
        // Arrange
        var root = JsonNode.Parse("""{"definitions":[{"type":"string"}]}""")!;

        var reference = new JsonObject { ["$ref"] = pointer };

        // Act
        var action = () => JsonSchemaReferenceResolver.Resolve(reference, root);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }
}
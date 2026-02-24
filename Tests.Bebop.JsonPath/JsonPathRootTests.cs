using System.Text.Json;
using JsonPathType = Bebop.JsonPath.JsonPath;

namespace Tests.Bebop.JsonPath;

/// <summary>
/// Tests for the JsonPath.Root static property.
/// </summary>
public sealed class JsonPathRootTests
{
    [Fact]
    public void Root_ReturnsValidJsonPath()
    {
        // Act
        var root = JsonPathType.Root;

        // Assert - JsonPath is a struct, so just verify we can access it
        Assert.Equal("$", root.ToString());
    }

    [Fact]
    public void Root_ToStringReturnsDollar()
    {
        // Act
        var root = JsonPathType.Root;

        // Assert
        Assert.Equal("$", root.ToString());
    }

    [Fact]
    public void Root_EqualsParseOfDollar()
    {
        // Arrange
        var parsed = JsonPathType.Parse("$");

        // Act
        var root = JsonPathType.Root;

        // Assert
        Assert.Equal(parsed, root);
        Assert.True(root == parsed);
        Assert.False(root != parsed);
    }

    [Fact]
    public void Root_EvaluateReturnsRootElement()
    {
        // Arrange
        var json = """{"a": 1, "b": 2}""";
        using var doc = JsonDocument.Parse(json);
        var root = JsonPathType.Root;

        // Act
        var result = root.Evaluate(doc.RootElement);

        // Assert
        Assert.NotNull(result);
        var elements = Assert.IsType<JsonElement[]>(result);
        Assert.Single(elements);
        Assert.Equal(JsonValueKind.Object, elements[0].ValueKind);
        Assert.Equal(json, elements[0].GetRawText());
    }

    [Fact]
    public void Root_PropertyChaining_CreatesCorrectPath()
    {
        // Arrange
        var json = """{"store": {"book": {"title": "Test Book"}}}""";
        using var doc = JsonDocument.Parse(json);

        // Act
        var path = JsonPathType.Root.Property("store").Property("book").Property("title");

        // Assert
        Assert.Equal("$['store']['book']['title']", path.ToString());
        
        var result = path.Evaluate(doc.RootElement);
        var elements = Assert.IsType<JsonElement[]>(result);
        Assert.Single(elements);
        Assert.Equal("\"Test Book\"", elements[0].GetRawText());
    }

    [Fact]
    public void Root_ArrayIndexChaining_CreatesCorrectPath()
    {
        // Arrange
        var json = """[10, 20, 30]""";
        using var doc = JsonDocument.Parse(json);

        // Act
        var path = JsonPathType.Root.ArrayIndex(1);

        // Assert
        Assert.Equal("$[1]", path.ToString());
        
        var result = path.Evaluate(doc.RootElement);
        var elements = Assert.IsType<JsonElement[]>(result);
        Assert.Single(elements);
        Assert.Equal(20, elements[0].GetInt32());
    }

    [Fact]
    public void Root_MixedChaining_CreatesCorrectPath()
    {
        // Arrange
        var json = """{"items": [{"name": "Item1"}, {"name": "Item2"}]}""";
        using var doc = JsonDocument.Parse(json);

        // Act
        var path = JsonPathType.Root.Property("items").ArrayIndex(0).Property("name");

        // Assert
        Assert.Equal("$['items'][0]['name']", path.ToString());
        
        var result = path.Evaluate(doc.RootElement);
        var elements = Assert.IsType<JsonElement[]>(result);
        Assert.Single(elements);
        Assert.Equal("\"Item1\"", elements[0].GetRawText());
    }

    [Fact]
    public void Root_IsSameInstanceOnMultipleCalls()
    {
        // Act
        var root1 = JsonPathType.Root;
        var root2 = JsonPathType.Root;

        // Assert
        Assert.Equal(root1, root2);
        Assert.True(root1 == root2);
    }

    [Fact]
    public void Root_WithComplexJson_EvaluatesCorrectly()
    {
        // Arrange
        var json = """
        {
          "store": {
            "book": [
              { "title": "Book 1", "price": 10 },
              { "title": "Book 2", "price": 20 }
            ],
            "bicycle": { "color": "red", "price": 100 }
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = JsonPathType.Root.Evaluate(doc.RootElement);

        // Assert
        var elements = Assert.IsType<JsonElement[]>(result);
        Assert.Single(elements);
        Assert.Equal(JsonValueKind.Object, elements[0].ValueKind);
        Assert.True(elements[0].TryGetProperty("store", out _));
    }

    [Fact]
    public void Root_HashCodeMatchesParsedDollar()
    {
        // Arrange
        var parsed = JsonPathType.Parse("$");
        var root = JsonPathType.Root;

        // Act & Assert
        Assert.Equal(parsed.GetHashCode(), root.GetHashCode());
    }

    [Fact]
    public void Root_CanBeUsedInCollections()
    {
        // Arrange & Act
        var paths = new HashSet<JsonPathType>
        {
            JsonPathType.Root,
            JsonPathType.Parse("$"),
            JsonPathType.Root.Property("a")
        };

        // Assert
        Assert.Equal(2, paths.Count); // Root and Parse("$") should be the same
    }
}

# Bebop.JsonPath

[![Build and Test](https://github.com/cptjazz/Bebop.JsonPath/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/cptjazz/Bebop.JsonPath/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Bebop.JsonPath.svg)](https://www.nuget.org/packages/Bebop.JsonPath/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bebop.JsonPath.svg)](https://www.nuget.org/packages/Bebop.JsonPath/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Coverage Status](https://coveralls.io/repos/github/cptjazz/Bebop.JsonPath/badge.svg?branch=main)](https://coveralls.io/github/cptjazz/Bebop.JsonPath?branch=main)

An RFC 9535 compliant JSONPath implementation for .NET.

## Features

- **RFC 9535 Compliant**: Full implementation of the [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535) JSONPath standard
- **Type Safe**: Works seamlessly with `System.Text.Json.JsonElement`
- **Zero Dependencies**: No external dependencies (except System.Text.Json for .NET 8)
- **Thoroughly Tested**: Passes all JSONPath Compliance Test Suite cases (703+ tests)
- **Multi-targeting**: Supports .NET 8.0 and .NET 10.0

## Installation

Install via NuGet:

```bash
dotnet add package Bebop.JsonPath
```

Or via Package Manager:

```powershell
Install-Package Bebop.JsonPath
```

## Quick Start

```csharp
using System.Text.Json;
using Bebop.JsonPath;

// Parse a JSONPath query
var path = JsonPath.Parse("$.store.book[?@.price < 10].title");

// Evaluate against a JSON document
var json = """
{
  "store": {
    "book": [
      { "title": "Sayings of the Century", "price": 8.95 },
      { "title": "Sword of Honour", "price": 12.99 },
      { "title": "Moby Dick", "price": 8.99 }
    ]
  }
}
""";

using var document = JsonDocument.Parse(json);
var result = path.Evaluate(document.RootElement);

// Result is a JsonElement[] containing matching nodes
if (result is JsonElement[] elements)
{
    foreach (var element in elements)
    {
        Console.WriteLine(element.GetString());
        // Output:
        // Sayings of the Century
        // Moby Dick
    }
}
```

## Usage Examples

### Basic Selectors

```csharp
var json = """{ "store": { "book": [{"title": "Book 1"}, {"title": "Book 2"}] } }""";
using var doc = JsonDocument.Parse(json);

// Name selector (object property)
var path1 = JsonPath.Parse("$.store.book");
var books = path1.Evaluate(doc.RootElement);

// Index selector
var path2 = JsonPath.Parse("$.store.book[0]");
var firstBook = path2.Evaluate(doc.RootElement);

// Wildcard selector
var path3 = JsonPath.Parse("$.store.book[*].title");
var titles = path3.Evaluate(doc.RootElement);
```

### Array Slices

```csharp
var json = """["a", "b", "c", "d", "e"]""";
using var doc = JsonDocument.Parse(json);

// Get elements 1-3 (indices 1, 2)
var path1 = JsonPath.Parse("$[1:3]");
// Result: ["b", "c"]

// Get every other element
var path2 = JsonPath.Parse("$[::2]");
// Result: ["a", "c", "e"]

// Reverse array
var path3 = JsonPath.Parse("$[::-1]");
// Result: ["e", "d", "c", "b", "a"]
```

### Descendant Selector

```csharp
var json = """
{
  "store": {
    "book": [
      { "title": "Book 1", "author": { "name": "Author 1" } },
      { "title": "Book 2", "author": { "name": "Author 2" } }
    ]
  }
}
""";
using var doc = JsonDocument.Parse(json);

// Get all "name" values at any depth
var path = JsonPath.Parse("$..name");
var names = path.Evaluate(doc.RootElement);
// Result: ["Author 1", "Author 2"]
```

### Filter Expressions

```csharp
var json = """
{
  "products": [
    { "name": "Product 1", "price": 10, "inStock": true },
    { "name": "Product 2", "price": 25, "inStock": false },
    { "name": "Product 3", "price": 15, "inStock": true }
  ]
}
""";
using var doc = JsonDocument.Parse(json);

// Filter by price
var path1 = JsonPath.Parse("$.products[?@.price < 20]");

// Filter by existence
var path2 = JsonPath.Parse("$.products[?@.inStock]");

// Complex filters with logical operators
var path3 = JsonPath.Parse("$.products[?@.price < 20 && @.inStock]");
```

### Built-in Functions

RFC 9535 defines five built-in functions:

```csharp
// length() - string length, array length, or object member count
var path1 = JsonPath.Parse("$.items[?length(@.name) > 5]");

// count() - count nodes in a nodelist
var path2 = JsonPath.Parse("$.sections[?count(@.items) > 3]");

// match() - full string regex match
var path3 = JsonPath.Parse("$.users[?match(@.email, '.*@example\\.com')]");

// search() - substring regex match
var path4 = JsonPath.Parse("$.logs[?search(@.message, 'error')]");

// value() - extract value from single-node nodelist
var path5 = JsonPath.Parse("$.config[?value(@..enabled) == true]");
```

### Programmatic Construction

```csharp
// Build paths programmatically using Parse
var path = JsonPath.Parse("$")
    .Property("store")
    .Property("book")
    .Index(0)
    .Property("title");

// Or using the Root property
var path2 = JsonPath.Root
    .Property("store")
    .Property("book")
    .ArrayIndex(0)
    .Property("title");

// Both are equivalent to: $.store.book[0].title
```

## API Reference

### `JsonPath` Struct

#### Properties

- `static JsonPath Root` - Gets a JsonPath representing the root identifier "$"

#### Methods

- `static JsonPath Parse(string path)` - Parses a JSONPath query string (throws `FormatException` if invalid)
- `object? Evaluate(JsonElement jsonDocument)` - Evaluates the query and returns a `JsonElement[]` or `null`
- `bool TryEvaluate(JsonElement jsonDocument, out object? result)` - Tries to evaluate the query
- `JsonPath Property(string propertyName)` - Appends a property (name) selector
- `JsonPath Index(int index)` - Appends an array index selector
- `string ToString()` - Returns the original query string

## RFC 9535 Compliance

This implementation fully complies with [RFC 9535 (JSONPath: Query Expressions for JSON)](https://www.rfc-editor.org/rfc/rfc9535), including:

- Root identifier `$`
- Child segments `[...]` and `.property`
- Descendant segments `..`
- Name selectors with quoted strings
- Wildcard selector `*`
- Index selectors (positive and negative)
- Array slice selectors `[start:end:step]`
- Filter expressions `?<expr>`
- Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical operators: `&&`, `||`, `!`
- All five built-in functions: `length`, `count`, `match`, `search`, `value`
- Proper handling of `null` semantics
- Normalized path generation
- I-JSON exact integer range validation

All features are validated against the [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite).

## Performance

Bebop.JsonPath is designed for performance with:

- Zero-allocation parsing for most common queries
- Efficient segment evaluation
- Minimal memory overhead
- Stack-based parser (ref struct)

Benchmarks are included in the `Benchmarks.Bebop.JsonPath` project.

## Requirements

- .NET 8.0 or later
- System.Text.Json (included with .NET 10.0+, referenced as package for .NET 8.0)

## Building from Source

```bash
# Clone the repository
git clone https://github.com/cptjazz/Bebop.JsonPath.git
cd Bebop.JsonPath

# Initialize submodules (for compliance test suite)
git submodule update --init --recursive

# Build
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Pack NuGet package
dotnet pack Bebop.JsonPath/Bebop.JsonPath.csproj --configuration Release
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Implements [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535) by S. Gössner, G. Normington, and C. Bormann
- Tested against the [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite)

## Related Projects

- [RFC 9535 Specification](https://www.rfc-editor.org/rfc/rfc9535)
- [JSONPath Comparison](https://cburgmer.github.io/json-path-comparison/)

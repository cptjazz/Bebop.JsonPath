using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bebop.JsonPath.Internal;

namespace Bebop.JsonPath;

public readonly struct JsonPath : IEquatable<JsonPath>
{
    private readonly ReadOnlyMemory<Segment> _segments;
    private readonly string _original;

    internal JsonPath(ReadOnlyMemory<Segment> segments, string original)
    {
        _segments = segments;
        _original = original;
    }

    /// <summary>
    /// Gets a <see cref="JsonPath"/> representing the root identifier "$".
    /// </summary>
    public static JsonPath Root { get; } = new JsonPath(ReadOnlyMemory<Segment>.Empty, "$");

    /// <summary>
    /// Parses an RFC 9535 JSONPath query string.
    /// </summary>
    /// <exception cref="FormatException">The query is not well-formed or not valid.</exception>
    public static JsonPath Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var parser = new JsonPathParser(path.AsSpan());
        var segments = parser.Parse();
        return new JsonPath(segments, path);
    }

    /// <summary>
    /// Returns the original query string.
    /// </summary>
    public override string ToString() => _original ?? "$";

    /// <summary>
    /// Creates a new <see cref="JsonPath"/> with an appended property (name) selector.
    /// </summary>
    public JsonPath Property(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        var newSegments = new Segment[_segments.Length + 1];
        _segments.Span.CopyTo(newSegments);
        newSegments[^1] = new SingleSelectorSegment(new NameSelector(propertyName), false);

        string newOriginal = (_original ?? "$") + "['" + EscapeName(propertyName) + "']";
        return new JsonPath(newSegments, newOriginal);
    }

    /// <summary>
    /// Creates a new <see cref="JsonPath"/> with an appended index selector.
    /// </summary>
    public JsonPath Index(int index)
    {
        var newSegments = new Segment[_segments.Length + 1];
        _segments.Span.CopyTo(newSegments);
        newSegments[^1] = new SingleSelectorSegment(new IndexSelector(index), false);

        string newOriginal = (_original ?? "$") + "[" + index + "]";
        return new JsonPath(newSegments, newOriginal);
    }

    public override bool Equals(object? obj)
    {
        return obj is JsonPath other && Equals(other);
    }

    public bool Equals(JsonPath other)
    {
        return string.Equals(_original, other._original, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return (_original ?? "$").GetHashCode(StringComparison.Ordinal);
    }

    public static bool operator ==(JsonPath left, JsonPath right) => left.Equals(right);
    public static bool operator !=(JsonPath left, JsonPath right) => !left.Equals(right);

    /// <summary>
    /// Evaluates the JSONPath query against the given JSON element.
    /// Returns the resulting nodelist as <see cref="JsonElement"/>[], or null if evaluation fails.
    /// </summary>
    public IEnumerable<JsonElement> Evaluate(in JsonElement jsonDocument)
    {
        return TryEvaluate(jsonDocument, out var result)
            ? result
            : _Throw();

        [DoesNotReturn]
        static IEnumerable<JsonElement> _Throw()
        {
            throw new InvalidOperationException("Evaluation failed.");
        }
    }

    /// <summary>
    /// Tries to evaluate the JSONPath query against the given JSON element.
    /// </summary>
    public bool TryEvaluate(in JsonElement jsonDocument, out IEnumerable<JsonElement> result)
    {
        result = JsonPathEvaluator2.Evaluate(_segments.Span, in jsonDocument);
        return true;
    }

    private static string EscapeName(string name)
    {
        return name
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }
}

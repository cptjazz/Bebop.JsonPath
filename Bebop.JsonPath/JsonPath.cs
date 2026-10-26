using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bebop.JsonPath.Internal;

namespace Bebop.JsonPath;

public readonly struct JsonPath : IEquatable<JsonPath>
{
    private static readonly SearchValues<char> _escapeCharacters = SearchValues.Create(['\\', '\'']);

    private readonly IReadOnlyList<Segment> _segments;
    private readonly ReadOnlyMemory<char> _original;

    internal JsonPath(IReadOnlyList<Segment> segments, ReadOnlyMemory<char> original)
    {
        _segments = segments;
        _original = original;
    }

    /// <summary>
    /// Gets a <see cref="JsonPath"/> representing the root identifier "$".
    /// </summary>
    public static JsonPath Root { get; } = new([], "$".AsMemory());

    /// <summary>
    /// Parses an RFC 9535 JSONPath query string.
    /// </summary>
    /// <exception cref="FormatException">The query is not well-formed or not valid.</exception>
    public static JsonPath Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var parser = new JsonPathParser(path.AsSpan());
        var segments = parser.Parse();

        return new JsonPath(segments, path.AsMemory());
    }

    /// <summary>
    /// Returns the original query string.
    /// </summary>
    public override string ToString() => _original.IsEmpty ? "$" : new string(_original.Span);

    /// <summary>
    /// Creates a new <see cref="JsonPath"/> with an appended property (name) selector.
    /// </summary>
    public JsonPath Property(ReadOnlySpan<char> propertyName)
    {
        if (propertyName.Length == 0)
            _Throw();

        var newSegments = new Segment[_segments.Count + 1];
        _CopyTo(_segments, newSegments, 0);
        newSegments[^1] = new Segment([new NameSelector(propertyName)], false, false);

        var x = _original.IsEmpty ? "$" : _original.Span;

        string newOriginal;
        var first = propertyName.IndexOfAny(_escapeCharacters);

        if (first < 0)
        {
            newOriginal = string.Concat(x, "['", propertyName, "']");
        }
        else
        {
            scoped Span<char> escaped;

            if (propertyName.Length > 1024)
            {
                escaped = new char[first + (propertyName.Length - first) * 2 + 1];
            }
            else
            {
                escaped = stackalloc char[first + (propertyName.Length - first) * 2 + 1];
            }

            propertyName[..first].CopyTo(escaped);

            var targetIndex = first;
            for (var i = first; i < propertyName.Length; i++)
            {
                var c = propertyName[i];
                if (_escapeCharacters.Contains(c))
                {
                    escaped[targetIndex++] = '\\';
                }

                escaped[targetIndex++] = c;
            }

            newOriginal = string.Concat(x, "['", escaped[..targetIndex], "']");
        }

        return new JsonPath(newSegments, newOriginal.AsMemory());

        [DoesNotReturn]
        static void _Throw()
        {
            throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
        }
    }

    /// <summary>
    /// Creates a new <see cref="JsonPath"/> with an appended index selector.
    /// </summary>
    public JsonPath Index(int index)
    {
        var newSegments = new Segment[_segments.Count + 1];

        _CopyTo(_segments, newSegments, 0);
        newSegments[^1] = new Segment([IndexSelectorFactory.Create(index)], false, false);

        var x = _original.IsEmpty ? "$" : _original.Span;

        Span<char> indexStr = stackalloc char[32];
        index.TryFormat(indexStr, out var charsWritten);

        string newOriginal = string.Concat(x, "[", indexStr[..charsWritten], "]");
        return new JsonPath(newSegments, newOriginal.AsMemory());
    }


    private static void _CopyTo<T>(
        IReadOnlyList<T> source,
        T[] destination,
        int destinationIndex = 0)
    {
        switch (source)
        {
            case T[] array:
                Array.Copy(
                    array, 0,
                    destination, destinationIndex,
                    array.Length);
                break;

            case ICollection<T> collection:
                collection.CopyTo(destination, destinationIndex);
                break;

            default:
                for (int i = 0; i < source.Count; i++)
                    destination[destinationIndex + i] = source[i];
                break;
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is JsonPath other && Equals(other);
    }

    public bool Equals(JsonPath other)
    {
        return _original.Span.Equals(other._original.Span, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        var s = _original.IsEmpty ? "$" : _original.Span;
        return string.GetHashCode(s, StringComparison.Ordinal);
    }

    public static bool operator ==(JsonPath left, JsonPath right) => left.Equals(right);
    public static bool operator !=(JsonPath left, JsonPath right) => !left.Equals(right);

    /// <summary>
    /// Evaluates the JSONPath query against the given JSON element.
    /// Returns the resulting nodelist as <see cref="JsonElement"/>[], or null if evaluation fails.
    /// </summary>
    public IReadOnlyList<JsonElement> Evaluate(in JsonElement jsonDocument)
    {
        return TryEvaluate(jsonDocument, out var result)
            ? result
            : _Throw();

        [DoesNotReturn]
        static IReadOnlyList<JsonElement> _Throw()
        {
            throw new InvalidOperationException("Evaluation failed.");
        }
    }

    /// <summary>
    /// Tries to evaluate the JSONPath query against the given JSON element.
    /// </summary>
    public bool TryEvaluate(in JsonElement jsonDocument, out IReadOnlyList<JsonElement> result)
    {
        result = JsonPathEvaluator.Evaluate(_segments, in jsonDocument);
        return true;
    }
}

using System.Text.Json;
using JsonPathType = Bebop.JsonPath.JsonPath;

namespace Tests.Bebop.JsonPath.Compliance;

/// <summary>
/// Runs the RFC 9535 JSONPath Compliance Test Suite against the <see cref="JsonPathType"/> implementation.
/// </summary>
/// <remarks>
/// Test data is sourced from
/// <see href="https://github.com/jsonpath-standard/jsonpath-compliance-test-suite"/>.
/// </remarks>
public sealed class ComplianceTests
{
    /// <summary>
    /// Tests that a valid selector with a deterministic expected result produces exactly that result.
    /// </summary>
    [Theory]
    [ClassData(typeof(CtsDeterministicSelectorData))]
    public void ValidSelector_Deterministic(string name, string selector, string documentJson, string resultJson)
    {
        _ = name; // Used by xUnit for display only.

        using var document = JsonDocument.Parse(documentJson);
        var expectedNodelist = JsonSerializer.Deserialize<JsonElement[]>(resultJson)!;

        var path = JsonPathType.Parse(selector);
        var actual = path.Evaluate(document.RootElement);

        AssertNodelistEqual(expectedNodelist, actual);
    }

    /// <summary>
    /// Tests that a valid selector with non-deterministic ordering produces one of the acceptable results.
    /// </summary>
    [Theory]
    [ClassData(typeof(CtsNonDeterministicSelectorData))]
    public void ValidSelector_NonDeterministic(string name, string selector, string documentJson, string resultsJson)
    {
        _ = name;

        using var document = JsonDocument.Parse(documentJson);
        var acceptableResults = JsonSerializer.Deserialize<JsonElement[][]>(resultsJson)!;

        var path = JsonPathType.Parse(selector);
        var actual = path.Evaluate(document.RootElement);

        bool matchesAny = acceptableResults
            .Any(expected => NodelistEquals(expected, actual));

        Assert.True(matchesAny,
            $"Result did not match any of the {acceptableResults.Length} acceptable orderings. " +
            $"Selector: {selector}, Actual: {JsonSerializer.Serialize(actual)}");
    }

    /// <summary>
    /// Tests that an invalid selector is rejected during parsing.
    /// </summary>
    [Theory]
    [ClassData(typeof(CtsInvalidSelectorData))]
    public void InvalidSelector_IsRejected(string name, string selector)
    {
        _ = name;

        // The implementation must raise an error for invalid selectors.
        // We accept any exception type (ArgumentException, FormatException, etc.).
        var ex = Record.Exception(() => JsonPathType.Parse(selector));

        Assert.NotNull(ex);
    }

    /// <summary>
    /// Asserts that two nodelists (as JsonElement arrays) are structurally equal.
    /// </summary>
    private static void AssertNodelistEqual(JsonElement[] expected, object? actual)
    {
        Assert.NotNull(actual);

        // The implementation may return results as various types.
        // Normalize to a JsonElement array for comparison.
        var actualElements = NormalizeResult(actual);

        Assert.Equal(expected.Length, actualElements.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                JsonElementDeepEquals(expected[i], actualElements[i]),
                $"Mismatch at index {i}: expected {expected[i].GetRawText()}, got {actualElements[i].GetRawText()}");
        }
    }

    private static bool NodelistEquals(JsonElement[] expected, object? actual)
    {
        if (actual is null)
            return expected.Length == 0;

        var actualElements = NormalizeResult(actual);
        if (expected.Length != actualElements.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (!JsonElementDeepEquals(expected[i], actualElements[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Normalizes the evaluation result into an array of <see cref="JsonElement"/>.
    /// Adapt this method if the <see cref="JsonPathType.Evaluate"/> return type changes.
    /// </summary>
    private static JsonElement[] NormalizeResult(object result) => result switch
    {
        JsonElement[] elements => elements,
        JsonElement element => [element],
        IEnumerable<JsonElement> enumerable => enumerable.ToArray(),
        _ => JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(result)) ?? []
    };

    /// <summary>
    /// Deep-compares two <see cref="JsonElement"/> values for structural equality.
    /// </summary>
    private static bool JsonElementDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(a, b),
            JsonValueKind.Array => ArrayEquals(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private static bool ObjectEquals(JsonElement a, JsonElement b)
    {
        var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        if (aProps.Count != bProps.Count)
            return false;

        foreach (var (key, aValue) in aProps)
        {
            if (!bProps.TryGetValue(key, out var bValue))
                return false;
            if (!JsonElementDeepEquals(aValue, bValue))
                return false;
        }

        return true;
    }

    private static bool ArrayEquals(JsonElement a, JsonElement b)
    {
        int aLen = a.GetArrayLength();
        int bLen = b.GetArrayLength();

        if (aLen != bLen)
            return false;

        using var aEnum = a.EnumerateArray();
        using var bEnum = b.EnumerateArray();

        while (aEnum.MoveNext() && bEnum.MoveNext())
        {
            if (!JsonElementDeepEquals(aEnum.Current, bEnum.Current))
                return false;
        }

        return true;
    }
}

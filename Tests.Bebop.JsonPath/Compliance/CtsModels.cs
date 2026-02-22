using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tests.Bebop.JsonPath.Compliance;

/// <summary>
/// Root object of a CTS JSON file. Contains an array of test cases.
/// </summary>
internal sealed record CtsTestSuite(
    [property: JsonPropertyName("tests")] CtsTestCase[] Tests
);

/// <summary>
/// A single test case from the JSONPath Compliance Test Suite.
/// </summary>
/// <remarks>
/// Exactly one of the following shapes applies:
/// <list type="bullet">
///   <item><c>document</c> + <c>result</c> + <c>result_paths</c> — deterministic valid selector.</item>
///   <item><c>document</c> + <c>results</c> + <c>results_paths</c> — non-deterministic valid selector.</item>
///   <item><c>invalid_selector = true</c> — the selector is not valid JSONPath.</item>
/// </list>
/// </remarks>
internal sealed record CtsTestCase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("selector")]
    public required string Selector { get; init; }

    [JsonPropertyName("document")]
    public JsonElement? Document { get; init; }

    /// <summary>
    /// The single expected result nodelist (deterministic).
    /// </summary>
    [JsonPropertyName("result")]
    public JsonElement[]? Result { get; init; }

    /// <summary>
    /// Multiple acceptable result nodelists (non-deterministic ordering).
    /// </summary>
    [JsonPropertyName("results")]
    public JsonElement[][]? Results { get; init; }

    /// <summary>
    /// When <c>true</c>, the selector is expected to be rejected as invalid.
    /// </summary>
    [JsonPropertyName("invalid_selector")]
    public bool? InvalidSelector { get; init; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; init; }

    /// <summary>
    /// Normalized paths corresponding to <see cref="Result"/>.
    /// </summary>
    [JsonPropertyName("result_paths")]
    public string[]? ResultPaths { get; init; }

    /// <summary>
    /// Multiple acceptable normalized path sets (non-deterministic).
    /// </summary>
    [JsonPropertyName("results_paths")]
    public string[][]? ResultsPaths { get; init; }

    /// <summary>
    /// Whether this test case expects the selector to be invalid.
    /// </summary>
    [JsonIgnore]
    public bool IsInvalidSelector => InvalidSelector == true;

    /// <summary>
    /// Whether this test case has non-deterministic (multiple valid) results.
    /// </summary>
    [JsonIgnore]
    public bool IsNonDeterministic => Results is not null;
}

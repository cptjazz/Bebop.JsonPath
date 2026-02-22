using System.Reflection;
using System.Text.Json;

namespace Tests.Bebop.JsonPath.Compliance;

/// <summary>
/// xUnit data source that loads test cases from the JSONPath Compliance Test Suite
/// JSON files and yields them as <c>[Theory]</c> row data.
/// </summary>
/// <remarks>
/// Each row yields: <c>(string name, string selector, JsonElement? document,
/// JsonElement[]? result, JsonElement[][]? results)</c>.
/// </remarks>
internal sealed class CtsValidSelectorData : TheoryData<string, string, string, string>
{
    public CtsValidSelectorData()
    {
        foreach (var testCase in CtsTestFileReader.ReadAllValid())
        {
            // Serialize document and result/results to strings so xUnit can display them.
            string documentJson = testCase.Document?.GetRawText() ?? "null";
            string expectedJson = testCase.IsNonDeterministic
                ? JsonSerializer.Serialize(testCase.Results, CtsTestFileReader.SerializerOptions)
                : JsonSerializer.Serialize(testCase.Result, CtsTestFileReader.SerializerOptions);

            Add(testCase.Name, testCase.Selector, documentJson, expectedJson);
        }
    }
}

/// <summary>
/// xUnit data source that yields all invalid-selector test cases.
/// Each row yields: <c>(string name, string selector)</c>.
/// </summary>
internal sealed class CtsInvalidSelectorData : TheoryData<string, string>
{
    public CtsInvalidSelectorData()
    {
        foreach (var testCase in CtsTestFileReader.ReadAllInvalid())
        {
            Add(testCase.Name, testCase.Selector);
        }
    }
}

/// <summary>
/// xUnit data source that yields non-deterministic valid-selector test cases.
/// Each row yields: <c>(string name, string selector, string documentJson, string resultsJson)</c>.
/// The <c>resultsJson</c> is a JSON array of arrays, each representing one acceptable result ordering.
/// </summary>
internal sealed class CtsNonDeterministicSelectorData : TheoryData<string, string, string, string>
{
    public CtsNonDeterministicSelectorData()
    {
        foreach (var testCase in CtsTestFileReader.ReadAllValid().Where(t => t.IsNonDeterministic))
        {
            string documentJson = testCase.Document?.GetRawText() ?? "null";
            string resultsJson = JsonSerializer.Serialize(testCase.Results, CtsTestFileReader.SerializerOptions);

            Add(testCase.Name, testCase.Selector, documentJson, resultsJson);
        }
    }
}

/// <summary>
/// xUnit data source that yields deterministic valid-selector test cases.
/// Each row yields: <c>(string name, string selector, string documentJson, string resultJson)</c>.
/// </summary>
internal sealed class CtsDeterministicSelectorData : TheoryData<string, string, string, string>
{
    public CtsDeterministicSelectorData()
    {
        foreach (var testCase in CtsTestFileReader.ReadAllValid().Where(t => !t.IsNonDeterministic))
        {
            string documentJson = testCase.Document?.GetRawText() ?? "null";
            string resultJson = JsonSerializer.Serialize(testCase.Result, CtsTestFileReader.SerializerOptions);

            Add(testCase.Name, testCase.Selector, documentJson, resultJson);
        }
    }
}

/// <summary>
/// Reads all CTS JSON files from the output directory and deserializes them.
/// </summary>
internal static class CtsTestFileReader
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private static readonly Lazy<CtsTestCase[]> AllTestCases = new(LoadAll);

    /// <summary>
    /// Returns all valid-selector test cases (both deterministic and non-deterministic).
    /// </summary>
    internal static IEnumerable<CtsTestCase> ReadAllValid()
        => AllTestCases.Value.Where(t => !t.IsInvalidSelector);

    /// <summary>
    /// Returns all invalid-selector test cases.
    /// </summary>
    internal static IEnumerable<CtsTestCase> ReadAllInvalid()
        => AllTestCases.Value.Where(t => t.IsInvalidSelector);

    private static CtsTestCase[] LoadAll()
    {
        // Use the combined cts.json file which contains all tests.
        string ctsPath = FindCtsJson();
        string json = File.ReadAllText(ctsPath);

        var suite = JsonSerializer.Deserialize<CtsTestSuite>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize {ctsPath}");

        return suite.Tests;
    }

    private static string FindCtsJson()
    {
        // Look relative to the test assembly location (output directory).
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // Try the Content-copied location first.
        string path = Path.Combine(assemblyDir, "cts", "cts.json");
        if (File.Exists(path))
            return path;

        // Fallback: look up the directory tree for the submodule.
        string? dir = assemblyDir;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "jsonpath-compliance-test-suite", "cts.json");
            if (File.Exists(candidate))
                return candidate;

            // Also check the test project directory directly.
            candidate = Path.Combine(dir, "Tests.Bebop.JsonPath", "jsonpath-compliance-test-suite", "cts.json");
            if (File.Exists(candidate))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(
            "Could not find cts.json from the jsonpath-compliance-test-suite. " +
            "Ensure the git submodule is initialized and the project is built.");
    }
}

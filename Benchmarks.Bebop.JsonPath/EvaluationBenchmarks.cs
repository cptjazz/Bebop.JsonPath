using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Json.Path;
using BebopJsonPath = Bebop.JsonPath.JsonPath;
using JeJsonPath = Json.Path.JsonPath;

namespace Benchmarks.Bebop.JsonPath;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EvaluationBenchmarks
{
    private const string BookStoreJson = """
        {
          "store": {
            "book": [
              { "category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95 },
              { "category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99 },
              { "category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "isbn": "0-553-21311-3", "price": 8.99 },
              { "category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "isbn": "0-395-19395-8", "price": 22.99 }
            ],
            "bicycle": { "color": "red", "price": 19.95 }
          }
        }
        """;

    private const string LargeArrayJson = "[" +
        "{\"id\":0,\"name\":\"item0\",\"value\":0,\"active\":true}," +
        "{\"id\":1,\"name\":\"item1\",\"value\":10,\"active\":false}," +
        "{\"id\":2,\"name\":\"item2\",\"value\":20,\"active\":true}," +
        "{\"id\":3,\"name\":\"item3\",\"value\":30,\"active\":false}," +
        "{\"id\":4,\"name\":\"item4\",\"value\":40,\"active\":true}," +
        "{\"id\":5,\"name\":\"item5\",\"value\":50,\"active\":false}," +
        "{\"id\":6,\"name\":\"item6\",\"value\":60,\"active\":true}," +
        "{\"id\":7,\"name\":\"item7\",\"value\":70,\"active\":false}," +
        "{\"id\":8,\"name\":\"item8\",\"value\":80,\"active\":true}," +
        "{\"id\":9,\"name\":\"item9\",\"value\":90,\"active\":false}," +
        "{\"id\":10,\"name\":\"item10\",\"value\":100,\"active\":true}," +
        "{\"id\":11,\"name\":\"item11\",\"value\":110,\"active\":false}," +
        "{\"id\":12,\"name\":\"item12\",\"value\":120,\"active\":true}," +
        "{\"id\":13,\"name\":\"item13\",\"value\":130,\"active\":false}," +
        "{\"id\":14,\"name\":\"item14\",\"value\":140,\"active\":true}," +
        "{\"id\":15,\"name\":\"item15\",\"value\":150,\"active\":false}," +
        "{\"id\":16,\"name\":\"item16\",\"value\":160,\"active\":true}," +
        "{\"id\":17,\"name\":\"item17\",\"value\":170,\"active\":false}," +
        "{\"id\":18,\"name\":\"item18\",\"value\":180,\"active\":true}," +
        "{\"id\":19,\"name\":\"item19\",\"value\":190,\"active\":false}" +
        "]";

    // Pre-parsed paths and documents for evaluation benchmarks
    private JsonElement _bookStoreElement;
    private JsonNode _bookStoreNode = null!;
    private JsonElement _largeArrayElement;
    private JsonNode _largeArrayNode = null!;

    private BebopJsonPath _bebopSimple;
    private BebopJsonPath _bebopDescendant;
    private BebopJsonPath _bebopSlice;
    private BebopJsonPath _bebopFilter;
    private BebopJsonPath _bebopComplexFilter;
    private BebopJsonPath _bebopWildcard;

    private JeJsonPath _jeSimple = null!;
    private JeJsonPath _jeDescendant = null!;
    private JeJsonPath _jeSlice = null!;
    private JeJsonPath _jeFilter = null!;
    private JeJsonPath _jeComplexFilter = null!;
    private JeJsonPath _jeWildcard = null!;

    [GlobalSetup]
    public void Setup()
    {
        var bookDoc = JsonDocument.Parse(BookStoreJson);
        _bookStoreElement = bookDoc.RootElement.Clone();

        var arrayDoc = JsonDocument.Parse(LargeArrayJson);
        _largeArrayElement = arrayDoc.RootElement.Clone();

        _bookStoreNode = JsonNode.Parse(BookStoreJson)!;
        _largeArrayNode = JsonNode.Parse(LargeArrayJson)!;

        _bebopSimple = BebopJsonPath.Parse("$.store.book[0].title");
        _bebopDescendant = BebopJsonPath.Parse("$.store..price");
        _bebopSlice = BebopJsonPath.Parse("$[0:10:2]");
        _bebopFilter = BebopJsonPath.Parse("$[?@.price<10]");
        _bebopComplexFilter = BebopJsonPath.Parse("$[?@.value>50 && @.active==true]");
        _bebopWildcard = BebopJsonPath.Parse("$.store.book[*].author");

        _jeSimple = JeJsonPath.Parse("$.store.book[0].title");
        _jeDescendant = JeJsonPath.Parse("$.store..price");
        _jeSlice = JeJsonPath.Parse("$[0:10:2]");
        _jeFilter = JeJsonPath.Parse("$[?@.price<10]");
        _jeComplexFilter = JeJsonPath.Parse("$[?@.value>50 && @.active==true]");
        _jeWildcard = JeJsonPath.Parse("$.store.book[*].author");
    }

    // ── Simple property access ────────────────────────────────────────────

    [BenchmarkCategory("SimpleEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_Simple() => _bebopSimple.Evaluate(_bookStoreElement);

    [BenchmarkCategory("SimpleEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_Simple() => _jeSimple.Evaluate(_bookStoreNode).Matches;

    // ── Descendant (recursive) ────────────────────────────────────────────

    [BenchmarkCategory("DescendantEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_Descendant() => _bebopDescendant.Evaluate(_bookStoreElement);

    [BenchmarkCategory("DescendantEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_Descendant() => _jeDescendant.Evaluate(_bookStoreNode).Matches;

    // ── Slice on array ────────────────────────────────────────────────────

    [BenchmarkCategory("SliceEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_Slice() => _bebopSlice.Evaluate(_largeArrayElement);

    [BenchmarkCategory("SliceEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_Slice() => _jeSlice.Evaluate(_largeArrayNode).Matches;

    // ── Filter on bookstore ───────────────────────────────────────────────

    [BenchmarkCategory("FilterEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_Filter() => _bebopFilter.Evaluate(_bookStoreElement);

    [BenchmarkCategory("FilterEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_Filter() => _jeFilter.Evaluate(_bookStoreNode).Matches;

    // ── Complex filter on large array ─────────────────────────────────────

    [BenchmarkCategory("ComplexFilterEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_ComplexFilter() => _bebopComplexFilter.Evaluate(_largeArrayElement);

    [BenchmarkCategory("ComplexFilterEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_ComplexFilter() => _jeComplexFilter.Evaluate(_largeArrayNode).Matches;

    // ── Wildcard ──────────────────────────────────────────────────────────

    [BenchmarkCategory("WildcardEval"), Benchmark(Baseline = true)]
    public object? Bebop_Eval_Wildcard() => _bebopWildcard.Evaluate(_bookStoreElement);

    [BenchmarkCategory("WildcardEval"), Benchmark]
    public NodeList? JsonPathNet_Eval_Wildcard() => _jeWildcard.Evaluate(_bookStoreNode).Matches;
}

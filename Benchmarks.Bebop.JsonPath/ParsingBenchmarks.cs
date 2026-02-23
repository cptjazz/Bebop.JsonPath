using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BebopJsonPath = Bebop.JsonPath.JsonPath;
using JeJsonPath = Json.Path.JsonPath;

namespace Benchmarks.Bebop.JsonPath;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ParsingBenchmarks
{
    // ── Simple paths ──────────────────────────────────────────────────────

    [BenchmarkCategory("Simple"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_Simple() => BebopJsonPath.Parse("$.store.book[0].title");

    [BenchmarkCategory("Simple"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_Simple() => JeJsonPath.Parse("$.store.book[0].title");

    // ── Wildcard + descendant ─────────────────────────────────────────────

    [BenchmarkCategory("Descendant"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_Descendant() => BebopJsonPath.Parse("$.store..price");

    [BenchmarkCategory("Descendant"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_Descendant() => JeJsonPath.Parse("$.store..price");

    // ── Slice ─────────────────────────────────────────────────────────────

    [BenchmarkCategory("Slice"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_Slice() => BebopJsonPath.Parse("$.items[1:10:2]");

    [BenchmarkCategory("Slice"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_Slice() => JeJsonPath.Parse("$.items[1:10:2]");

    // ── Filter expression ─────────────────────────────────────────────────

    [BenchmarkCategory("Filter"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_Filter() => BebopJsonPath.Parse("$.store.book[?@.price<10]");

    [BenchmarkCategory("Filter"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_Filter() => JeJsonPath.Parse("$.store.book[?@.price<10]");

    // ── Complex filter with functions ─────────────────────────────────────

    [BenchmarkCategory("ComplexFilter"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_ComplexFilter() => BebopJsonPath.Parse("$.store.book[?@.price<10 && length(@.title)>3]");

    [BenchmarkCategory("ComplexFilter"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_ComplexFilter() => JeJsonPath.Parse("$.store.book[?@.price<10 && length(@.title)>3]");

    // ── Multiple selectors ────────────────────────────────────────────────

    [BenchmarkCategory("MultiSelector"), Benchmark(Baseline = true)]
    public BebopJsonPath Bebop_Parse_MultiSelector() => BebopJsonPath.Parse("$.store.book[0,1,2]['title','author']");

    [BenchmarkCategory("MultiSelector"), Benchmark]
    public JeJsonPath JsonPathNet_Parse_MultiSelector() => JeJsonPath.Parse("$.store.book[0,1,2]['title','author']");
}

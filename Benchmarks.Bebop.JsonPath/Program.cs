using BenchmarkDotNet.Running;
using Benchmarks.Bebop.JsonPath;

BenchmarkSwitcher.FromTypes([
    typeof(ParsingBenchmarks),
    typeof(EvaluationBenchmarks)
]).Run(args);

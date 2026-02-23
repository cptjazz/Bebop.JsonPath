using BenchmarkDotNet.Running;
using Benchmarks.Bebop.JsonPath;

BenchmarkRunner.Run([
    typeof(ParsingBenchmarks),
    typeof(EvaluationBenchmarks)
]);

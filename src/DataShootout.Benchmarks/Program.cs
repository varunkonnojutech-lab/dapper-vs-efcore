using BenchmarkDotNet.Running;
using DataShootout.Benchmarks;

// Run every benchmark against BOTH stores (see the [Params] on StoreKind).
// Always Release:  dotnet run -c Release --project src/DataShootout.Benchmarks
// Results land in BenchmarkDotNet.Artifacts/ — the summary table is what you
// paste into a PR discussion or a blog post.
BenchmarkRunner.Run<StoreBenchmarks>();

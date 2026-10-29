using BenchmarkDotNet.Running;
using Squill.Bench;

BenchmarkSwitcher.FromAssembly(typeof(TokenizeBenchmarks).Assembly).Run(args);

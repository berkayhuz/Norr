// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using BenchmarkDotNet.Running;

namespace Norr.PerformanceMonitor.Benchmarks;

/// <summary>
/// Entry point for running all <c>BenchmarkDotNet</c> benchmarks
/// in the <see cref="Norr.PerformanceMonitor.Benchmarks"/> assembly.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. Runs all benchmarks in the current assembly.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments passed to BenchmarkDotNet.
    /// For example, use <c>--filter</c> to run specific benchmarks.
    /// </param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

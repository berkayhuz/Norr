# Norr.PerformanceMonitor.Benchmarks

This project contains **benchmark tests** for the `Norr.PerformanceMonitor` library using [BenchmarkDotNet](https://benchmarkdotnet.org/).  
It is intended to measure the performance impact of different monitoring strategies and instrumentation approaches.

---

## Project structure

- **Benchmarks/** — Contains benchmark classes for various monitoring scenarios.
- **Program.cs** — Entry point that configures and runs benchmarks via BenchmarkDotNet.

---

## Requirements

- .NET 9 SDK
- BenchmarkDotNet NuGet package

---

## Running the benchmarks

From the root of this project, run:

```bash
dotnet run -c Release
```

BenchmarkDotNet will automatically:
- Warm up each benchmark
- Run multiple iterations
- Provide detailed statistics (mean, error, stddev)
- Export results in console, markdown, and CSV formats

---

## Example output

```text
| Method                  | Mean     | Error   | StdDev  | Allocated |
|------------------------ |---------:|--------:|--------:|----------:|
| MeasureOperationBasic   | 120.5 ns |  2.45 ns|  4.52 ns|      64 B |
| MeasureOperationAdvanced| 145.3 ns |  3.10 ns|  6.12 ns|      72 B |
```

> Times and allocations are for demonstration only; your results may vary depending on system and environment.

---

## Notes

- Ensure that you run in `Release` mode — Debug mode will significantly distort results.
- Avoid running other heavy CPU processes during benchmarks to ensure accuracy.
- BenchmarkDotNet creates isolated processes to minimize noise.

---

## License

MIT © Norr

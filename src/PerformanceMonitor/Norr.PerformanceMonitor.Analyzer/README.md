# Norr.PerformanceMonitor.Analyzer

**Norr.PerformanceMonitor.Analyzer** is a Roslyn analyzer for the `Norr.PerformanceMonitor` ecosystem.  
It provides compile-time checks, diagnostics, and code fixes to ensure correct usage of performance monitoring attributes and APIs.

---

## Features

- Validates correct usage of `[PerformanceMonitor]` and other related attributes.
- Warns when monitoring metadata is missing or inconsistent.
- Suggests fixes for common mistakes (e.g., missing operation names).
- Enforces best practices for `Norr.PerformanceMonitor` integration.

---

## Installation

```bash
dotnet add package Norr.PerformanceMonitor.Analyzer
```

When added as a package reference, analyzers run automatically during compilation in supported IDEs (Visual Studio, JetBrains Rider, VS Code with C# extension).

---

## Example diagnostics

| ID        | Severity | Message |
|-----------|----------|---------|
| `NORR001` | Warning  | PerformanceMonitor attribute is missing an OperationName. |
| `NORR002` | Warning  | Category should be specified for performance monitoring. |
| `NORR003` | Info     | Consider using ActivityTags constants for tag names. |

---

## Usage

Simply reference the analyzer package in your project along with `Norr.PerformanceMonitor` and write code as usual.

If an analyzer detects an issue, you’ll see a warning or suggestion in your IDE and build output. Some issues will have quick fixes you can apply automatically.

---

## Suppressing diagnostics

If you need to suppress a diagnostic, use standard [Roslyn suppression mechanisms](https://learn.microsoft.com/en-us/visualstudio/code-quality/in-source-suppression):

```csharp
[PerformanceMonitor(OperationName = "Order.Create")] // NORR001 suppressed
#pragma warning disable NORR001
public void CreateOrder() { }
#pragma warning restore NORR001
```

---

## Requirements

- .NET 9 SDK
- Norr.PerformanceMonitor core library
- Roslyn-compatible IDE or build process

---

## License

MIT © Norr

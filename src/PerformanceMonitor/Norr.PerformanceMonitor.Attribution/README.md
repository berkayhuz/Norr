# Norr.PerformanceMonitor.Attribution

**Norr.PerformanceMonitor.Attribution** provides attribute-based configuration for the `Norr.PerformanceMonitor` library.  
It allows you to annotate methods, classes, or assemblies with performance monitoring instructions without manually writing boilerplate code.

---

## Features

- Attribute-based performance monitoring.
- Easily attach monitoring metadata to methods and classes.
- Fine-grained control over monitoring behavior.
- Minimal intrusion into your business logic.

---

## Installation

```bash
dotnet add package Norr.PerformanceMonitor.Attribution
```

---

## Usage

### 1. Annotating a method

```csharp
using Norr.PerformanceMonitor.Attribution;

public class OrderService
{
    [PerformanceMonitor(OperationName = "Order.Create", Category = "Order", Component = "Service")]
    public void CreateOrder(Order order)
    {
        // Business logic here...
    }
}
```

### 2. Annotating a class

```csharp
[PerformanceMonitor(Category = "Order")]
public class OrderService
{
    [PerformanceMonitor(OperationName = "Order.Create", Component = "Service")]
    public void CreateOrder(Order order) { }

    [PerformanceMonitor(OperationName = "Order.Cancel", Component = "Service")]
    public void CancelOrder(int orderId) { }
}
```

---

## Attribute properties

| Property        | Type     | Description |
|----------------|----------|-------------|
| `OperationName`| `string` | Name of the monitored operation. |
| `Category`     | `string` | Logical category for the operation. |
| `Component`    | `string` | Component or subsystem name. |
| `Enabled`      | `bool`   | Whether monitoring is active for the target. |

---

## How it works

When the `Norr.PerformanceMonitor` system is initialized, it will scan your assemblies for the `[PerformanceMonitor]` attribute and automatically register monitoring for the annotated members.

This allows you to configure monitoring **declaratively** rather than programmatically.

---

## Requirements

- .NET 9
- Norr.PerformanceMonitor core library

---

## License

MIT © Norr

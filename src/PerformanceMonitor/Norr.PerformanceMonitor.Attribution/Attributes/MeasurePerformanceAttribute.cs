// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using Norr.PerformanceMonitor.Attribution.Generators;

namespace Norr.PerformanceMonitor.Attribution.Attributes;

/// <summary>
/// Marks a method or constructor for performance monitoring.  
/// When applied, the method/constructor will be wrapped by the source generator
/// to track execution time, memory allocation, and CPU usage automatically.
/// </summary>
/// <remarks>
/// This attribute is used in conjunction with <see cref="PerformanceSourceGenerator"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class MeasurePerformanceAttribute : Attribute
{
}

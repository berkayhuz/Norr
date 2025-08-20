// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System;
using System.Collections.Generic;
using System.Linq;

using OpenTelemetry.Resources;

namespace Norr.PerformanceMonitor.OpenTelemetry.Extensions;

/// <summary>
/// Convenience extensions for <see cref="ResourceBuilder"/> with null-safety.
/// </summary>
/// <remarks>
/// These helpers make it easier to populate OpenTelemetry resource attributes from
/// dictionaries that may contain <see langword="null"/> values.
/// </remarks>
public static class ResourceBuilderExtensions
{
    /// <summary>
    /// Adds all non-null entries from the provided <see cref="IDictionary{TKey, TValue}"/>
    /// to the target <see cref="ResourceBuilder"/> as resource attributes.
    /// </summary>
    /// <param name="resourceBuilder">The target <see cref="ResourceBuilder"/>.</param>
    /// <param name="attributes">
    /// A dictionary of attribute keys and values. Entries with <see langword="null"/> values
    /// are ignored.
    /// </param>
    /// <returns>The same <paramref name="resourceBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="resourceBuilder"/> or <paramref name="attributes"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// var rb = ResourceBuilder.CreateDefault()
    ///     .AddService("orders-api");
    ///
    /// var attrs = new Dictionary<string, object?>
    /// {
    ///     ["deployment.environment"] = "prod",
    ///     ["service.version"] = "1.2.3",
    ///     ["git.sha"] = null // will be skipped
    /// };
    ///
    /// rb.AddAttributesSafe(attrs);
    /// ]]></code>
    /// </example>
    public static ResourceBuilder AddAttributesSafe(
        this ResourceBuilder resourceBuilder,
        IDictionary<string, object?> attributes)
    {
        if (resourceBuilder is null)
            throw new ArgumentNullException(nameof(resourceBuilder));
        if (attributes is null)
            throw new ArgumentNullException(nameof(attributes));

        // Skip null values and project to the non-nullable value type expected by AddAttributes
        IEnumerable<KeyValuePair<string, object>> cleaned = attributes
            .Where(kv => kv.Value is not null)
            .Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value!));

        return resourceBuilder.AddAttributes(cleaned);
    }
}

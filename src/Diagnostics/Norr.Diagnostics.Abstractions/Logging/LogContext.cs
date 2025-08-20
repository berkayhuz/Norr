// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Norr.Diagnostics.Abstractions.Logging;

public sealed class LogContext : ILogContext, ICorrelationIdAccessor
{
    private readonly Dictionary<string, object?> _enrichment = new();

    public string? CorrelationId
    {
        get; set;
    }

    public IReadOnlyDictionary<string, object?> Enrichment
        => new ReadOnlyDictionary<string, object?>(_enrichment);

    public void Add(string key, object? value) => _enrichment[key] = value;
}

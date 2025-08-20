// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
namespace Norr.Diagnostics.Abstractions.Logging;

public interface ILogContext
{
    string? CorrelationId
    {
        get;
    }
    IReadOnlyDictionary<string, object?> Enrichment
    {
        get;
    }
}


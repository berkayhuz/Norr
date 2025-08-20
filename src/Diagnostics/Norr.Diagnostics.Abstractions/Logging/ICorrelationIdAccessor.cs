// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
namespace Norr.Diagnostics.Abstractions.Logging;

public interface ICorrelationIdAccessor
{
    string? CorrelationId
    {
        get; set;
    }
}

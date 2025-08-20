// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.Context;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId
    {
        get; set;
    }
}

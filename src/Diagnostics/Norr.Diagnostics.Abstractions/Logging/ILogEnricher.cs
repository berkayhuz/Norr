// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using Microsoft.Extensions.Logging;

namespace Norr.Diagnostics.Abstractions.Logging;

/// <summary>Scope temelinde loglara ek bilgi ekler.</summary>
public interface ILogEnricher
{
    IDisposable BeginScope(ILogger logger);
}

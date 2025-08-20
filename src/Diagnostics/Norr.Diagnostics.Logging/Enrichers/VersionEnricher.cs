// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.Enrichers;

public sealed class VersionEnricher : ILogEnricher
{
    private static readonly string _version =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new NoopDisposable();
        private NoopDisposable()
        {
        }
        public void Dispose()
        {
        }
    }

    public IDisposable BeginScope(ILogger logger) =>
        logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>("app_version", _version)
        }) ?? NoopDisposable.Instance;
}

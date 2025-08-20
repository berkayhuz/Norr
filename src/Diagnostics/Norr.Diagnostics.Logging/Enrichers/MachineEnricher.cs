// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.Enrichers;

public sealed class MachineEnricher : ILogEnricher
{
    private static readonly string _host = System.Net.Dns.GetHostName();

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
            new KeyValuePair<string, object?>("machine", _host)
        }) ?? NoopDisposable.Instance;
}

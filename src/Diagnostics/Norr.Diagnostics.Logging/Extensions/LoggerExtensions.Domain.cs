// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.Extensions;

public static class LoggerExtensionsDomain
{
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

    /// <summary>Enricher’ları tek scope’ta toplar.</summary>
    public static IDisposable BeginNorrScope(this ILogger logger, IServiceProvider sp)
    {
        var enrichers = sp.GetServices<ILogEnricher>().ToArray();
        if (enrichers.Length == 0)
            return NoopDisposable.Instance;

        var disposables = new IDisposable[enrichers.Length];
        for (int i = 0; i < enrichers.Length; i++)
            disposables[i] = enrichers[i].BeginScope(logger);

        return new Composite(disposables);
    }

    private sealed class Composite : IDisposable
    {
        private readonly IDisposable[] _items;
        public Composite(IDisposable[] items) => _items = items;
        public void Dispose()
        {
            for (int i = _items.Length - 1; i >= 0; i--)
                _items[i].Dispose();
        }
    }
}

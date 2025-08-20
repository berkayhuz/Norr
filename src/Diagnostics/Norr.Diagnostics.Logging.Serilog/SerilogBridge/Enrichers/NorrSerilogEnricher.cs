// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using Serilog.Core;
using Serilog.Events;

namespace Norr.Diagnostics.Logging.SerilogBridge.Enrichers;

public sealed class NorrSerilogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // İstersen correlation-id vs ekle
        // logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("norr", true));
    }
}

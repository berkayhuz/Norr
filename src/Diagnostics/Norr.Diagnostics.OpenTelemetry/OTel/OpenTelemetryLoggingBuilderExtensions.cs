// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using Microsoft.Extensions.Logging;

namespace Norr.Diagnostics.OpenTelemetry.Otel;

public static class OpenTelemetryLoggingBuilderExtensions
{
    public static ILoggingBuilder AddNorrOpenTelemetry(this ILoggingBuilder builder)
    {
        // Buraya ileride OTEL exporter ekleyeceÄŸiz.
        return builder;
    }
}


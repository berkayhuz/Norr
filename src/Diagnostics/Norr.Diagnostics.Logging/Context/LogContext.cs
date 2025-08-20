// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.Context;

public static class LogContextExtensions
{
    public static IDisposable? UseCorrelationScope(this ILogger logger, ICorrelationIdAccessor accessor) =>
        logger.BeginScope("{CorrelationId}", accessor.CorrelationId ?? string.Empty);
}

// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System;
using System.Collections.Generic;

namespace Norr.PerformanceMonitor.Configuration.Alerting;

/// <summary>
/// Security policy settings for outbound webhooks.
/// </summary>
public sealed class WebhookSecurityOptions
{
    /// <summary>Require HTTPS for webhook targets.</summary>
    public bool RequireHttps { get; init; } = true;

    /// <summary>
    /// Explicit allow-list of hostnames for webhook targets. Case-insensitive.
    /// Leave empty to allow any host (still subject to other rules).
    /// </summary>
    public ISet<string> AllowedHosts
    {
        get; init;
    } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Block webhook targets that resolve to private, link-local, or loopback addresses.
    /// </summary>
    public bool BlockPrivateAddresses { get; init; } = true;

    /// <summary>Allowed destination ports. If set and non-empty, only these are permitted.</summary>
    public ICollection<int>? AllowedPorts
    {
        get; init;
    }

    /// <summary>Blocked destination ports. If contains the port, it will be rejected.</summary>
    public ICollection<int>? BlockedPorts
    {
        get; init;
    }
}

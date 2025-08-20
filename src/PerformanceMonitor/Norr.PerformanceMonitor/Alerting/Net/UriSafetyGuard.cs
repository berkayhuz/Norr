// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Net;
using System.Net.Sockets;
using System.Threading; // CancellationToken

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;
using Norr.PerformanceMonitor.Configuration.Alerting;

namespace Norr.PerformanceMonitor.Alerting.Net;

/// <summary>
/// Guard for validating outbound webhook targets against <see cref="WebhookSecurityOptions"/>.
/// Synchronous and asynchronous variants are provided. Throws <see cref="InvalidOperationException"/> on failure.
/// </summary>
internal static class UriSafetyGuard
{
    /// <summary>
    /// Synchronous validation (kept for backward-compat and tests).
    /// Throws <see cref="InvalidOperationException"/> if invalid.
    /// </summary>
    public static void ValidateWebhookTarget(Uri uri, WebhookSecurityOptions? options, ILogger? logger = null)
    {
        if (uri is null)
            throw new ArgumentNullException(nameof(uri));
        options ??= new WebhookSecurityOptions();

        if (options.RequireHttps &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            logger?.PM().InsecureScheme(uri.Scheme);
            throw new InvalidOperationException("Webhook target must use HTTPS.");
        }

        // Allowlist (IDN normalize + case-insensitive)
        if (options.AllowedHosts is { Count: > 0 })
        {
            var normalizedHost = NormalizeHost(uri.Host);
            var set = BuildAllowedHostSet(options);
            if (!set.Contains(normalizedHost))
            {
                logger?.PM().HostNotAllowed(normalizedHost);
                throw new InvalidOperationException(
                    $"Webhook target host '{normalizedHost}' is not in the allowed hosts list.");
            }
        }

        // Port rules
        // BlockedPorts check only for explicitly specified ports (to avoid surprising failures on default ports)
        if (!uri.IsDefaultPort && options.BlockedPorts?.Contains(uri.Port) == true)
        {
            logger?.PM().PortBlocked(uri.Port);
            throw new InvalidOperationException($"Webhook target port {uri.Port} is blocked.");
        }

        // AllowedPorts (if provided) must include the effective port (explicit or default)
        if (options.AllowedPorts is { Count: > 0 } && !options.AllowedPorts.Contains(uri.Port))
        {
            logger?.PM().PortNotAllowed(uri.Port);
            throw new InvalidOperationException($"Webhook target port {uri.Port} is not in the allowed ports list.");
        }

        if (!options.BlockPrivateAddresses)
            return;

        // If IP literal, check without DNS
        if (IPAddress.TryParse(uri.Host, out var ipLiteral))
        {
            ThrowIfLoopbackOrPrivate(ipLiteral);
            return;
        }

        // Synchronous DNS (sufficient for tests)
        IPAddress[] addresses;
        try
        {
            // Use DnsSafeHost (punycode) for consistency with IDN
            addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            logger?.PM().DnsFailure(uri.DnsSafeHost, ex);
            throw new InvalidOperationException("DNS resolution failed for webhook target.", ex);
        }


        foreach (var addr in addresses)
            ThrowIfLoopbackOrPrivate(addr);
    }

    /// <summary>
    /// Async validation: uses non-blocking DNS. Throws <see cref="InvalidOperationException"/> if invalid.
    /// </summary>
    public static async Task ValidateWebhookTargetAsync(Uri uri, WebhookSecurityOptions? options, CancellationToken ct = default)
    {
        if (uri is null)
            throw new ArgumentNullException(nameof(uri));
        options ??= new WebhookSecurityOptions();

        if (options.RequireHttps &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Webhook target must use HTTPS.");
        }

        if (options.AllowedHosts is { Count: > 0 })
        {
            var normalizedHost = NormalizeHost(uri.Host);
            var set = BuildAllowedHostSet(options);
            if (!set.Contains(normalizedHost))
                throw new InvalidOperationException(
                    $"Webhook target host '{normalizedHost}' is not in the allowed hosts list.");
        }

        if (!uri.IsDefaultPort && options.BlockedPorts?.Contains(uri.Port) == true)
            throw new InvalidOperationException($"Webhook target port {uri.Port} is blocked.");
        if (options.AllowedPorts is { Count: > 0 } && !options.AllowedPorts.Contains(uri.Port))
            throw new InvalidOperationException($"Webhook target port {uri.Port} is not in the allowed ports list.");

        if (!options.BlockPrivateAddresses)
            return;

        if (IPAddress.TryParse(uri.Host, out var ipLiteral))
        {
            ThrowIfLoopbackOrPrivate(ipLiteral);
            return;
        }

        // --- Real async/await path ---
        IPAddress[] addresses;
        try
        {
            // Use DnsSafeHost (punycode) for consistency with IDN
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException or OperationCanceledException)
        {
            throw new InvalidOperationException("DNS resolution failed for webhook target.", ex);
        }

        foreach (var addr in addresses)
            ThrowIfLoopbackOrPrivate(addr);
    }

    /// <summary>Non-throwing helper.</summary>
    public static bool TryValidateWebhookTarget(Uri? uri, WebhookSecurityOptions? options, out string? reason)
    {
        try
        {
            ValidateWebhookTarget(uri!, options);
            reason = null;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static void ThrowIfLoopbackOrPrivate(IPAddress ip, ILogger? logger = null)
    {
        var ipStr = ip.ToString();

        if (IsLoopback(ip))
        {
            logger?.PM().LoopbackAddress(ipStr);
            throw new InvalidOperationException("Webhook target resolves to a loopback address.");
        }

        if (IsPrivate(ip))
        {
            logger?.PM().PrivateAddress(ipStr);
            throw new InvalidOperationException("Webhook target resolves to a private, link-local, multicast, unspecified, or CGNAT address.");
        }
    }

    private static bool IsLoopback(IPAddress ip)
    {
        // Normalize IPv4-mapped IPv6 to IPv4 first
        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return IPAddress.IsLoopback(ip);
    }

    private static bool IsPrivate(IPAddress ip)
    {
        // Normalize IPv4-mapped IPv6 to IPv4 for checks
        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var b = ip.GetAddressBytes();

        // Unspecified addresses
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
        {
            // Broadcast
            if (ip.Equals(IPAddress.Broadcast))
                return true;

            // Multicast 224.0.0.0/4
            if (b[0] >= 224 && b[0] <= 239)
                return true;

            // Private/link-local/CGNAT
            if (b[0] == 10)
                return true;                               // 10.0.0.0/8
            if (b[0] == 172 && b[1] is >= 16 and <= 31)
                return true;                               // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168)
                return true;                               // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254)
                return true;                               // 169.254.0.0/16 (link-local)
            if (b[0] == 100 && b[1] is >= 64 and <= 127)
                return true;                               // 100.64.0.0/10 (CGNAT)
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Multicast ff00::/8
            if (b[0] == 0xFF)
                return true;

            // ULA fc00::/7
            if ((b[0] & 0xFE) == 0xFC)
                return true;

            // Link-local fe80::/10
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)
                return true;
        }

        return false;
    }

    private static string NormalizeHost(string host)
        => new Uri($"https://{host}").IdnHost.ToLowerInvariant();

    private static HashSet<string> BuildAllowedHostSet(WebhookSecurityOptions options)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in options.AllowedHosts!)
        {
            if (string.IsNullOrWhiteSpace(h))
                continue;
            set.Add(NormalizeHost(h.Trim()));
        }
        return set;
    }
}

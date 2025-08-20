// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Telemetry;

/// <summary>
/// Utility for scrubbing (masking) and normalizing tag values in a <see cref="TagList"/> to
/// reduce Personally Identifiable Information (PII) exposure and cardinality in telemetry.
/// </summary>
/// <remarks>
/// <para>
/// This helper targets two primary goals:
/// </para>
/// <list type="number">
///   <item>
///     <description><b>Privacy &amp; Safety.</b> Potentially sensitive patterns (e.g., e‑mail addresses,
///     JWTs) are masked using a configurable <see cref="ScrubbingOptions.Mask"/> string.
///     All regular expressions are compiled, culture‑invariant, and have a <b>global timeout</b>
///     to minimize ReDoS risk. Where safe, patterns are compiled with
///     <see cref="RegexOptions.NonBacktracking"/> to further harden against pathological input.
///     </description>
///  Aşağıdaki Rx helper’ı, desen bu yapıları içeriyorsa NonBacktracking’i otomatik devre dışı bırakır. */
///   </item>
///   <item>
///     <description><b>Low Cardinality.</b> URL-/path‑like values are normalized by removing
///     query/fragment parts and replacing volatile segments such as GUIDs, long numbers,
///     and base64url‑like tokens with placeholders like <c>{id}</c> or <c>{tok}</c>.
///     A max-length truncation guard is also applied to limit cardinality explosion.
///     </description>
///   </item>
/// </list>
/// <para>
/// The class is stateless and thread‑safe. All operations are allocation‑conscious and suitable
/// for hot paths in middleware or exporters.
/// </para>
/// <para>
/// <b>Important:</b> Scrubbing policies are controlled by <see cref="ScrubbingOptions"/>. Defaults are
/// conservative and can be overridden per call.
/// </para>
/// </remarks>
/// <example>
/// Scrub a single value:
/// <code language="csharp"><![CDATA[
/// var input = "GET https://api.example.com/users/7f1e9a2b-1234-4c7a-9abc-55e6f0d2f111?token=abc#frag";
/// var safe  = TagScrubber.ScrubValue(input, new ScrubbingOptions
/// {
///     MaskSensitiveValues = true,
///     NormalizePaths = true,
///     MaxValueLength = 128
/// });
/// // safe => "/users/{id}"
/// ]]></code>
///
/// Scrub an entire <see cref="TagList"/> in-place:
/// <code language="csharp"><![CDATA[
/// var tags = new TagList
/// {
///     { "http.url", "https://api.example.com/orders/1234567890/details?debug=1" },
///     { "user.email", "alice@example.com" },
///     { "auth.jwt", "eyJhbGciOi..." }
/// };
///
/// TagScrubber.Apply(ref tags, ScrubbingOptions.Default);
/// // "http.url"   -> "/orders/{id}/details"
/// // "user.email" -> "***"
/// // "auth.jwt"   -> "***"
/// ]]></code>
/// </example>
internal static partial class TagScrubber
{
    /// <summary>
    /// Global timeout for all regex operations to mitigate Regular Expression DoS attacks.
    /// </summary>
    private static readonly TimeSpan _rxTimeout = TimeSpan.FromMilliseconds(100);

    // --- Regex factory ---------------------------------------------------------------

    /// <summary>
    /// Basit ve hızlı kontrol: desen negatif lookaround içeriyor mu?
    /// NonBacktracking ile uyumsuz olanlar: <c>(?! ...)</c> ve <c>(?&lt;! ...)</c>.
    /// (Kaçışları tam modellemeyen “best effort” string taraması bilinçli tercihtir.)
    /// </summary>
    private static bool ContainsNegativeLookaround(string pattern) =>
        pattern.Contains("(?!", StringComparison.Ordinal)
        || pattern.Contains("(?<!", StringComparison.Ordinal);

    /// <summary>
    /// Creates a hardened <see cref="Regex"/>:
    /// Compiled + CultureInvariant + ExplicitCapture (+ IgnoreCase) + timeout.
    /// Negatif lookaround içermeyen desenlerde ek olarak NonBacktracking açılır.
    /// </summary>
    private static Regex Rx(string pattern, bool ignoreCase = true)
    {
        var opts = RegexOptions.Compiled
                 | RegexOptions.CultureInvariant
                 | RegexOptions.ExplicitCapture;

        if (ignoreCase)
            opts |= RegexOptions.IgnoreCase;

        // NonBacktracking sadece güvenli olduğunda eklenir
        if (!ContainsNegativeLookaround(pattern))
        {
            opts |= RegexOptions.NonBacktracking;
        }

        return new Regex(pattern, opts, _rxTimeout);
    }

    // --- Sensitive/PII patterns ------------------------------------------------------

    /// <summary>E‑mail addresses (best‑effort; avoids catastrophic patterns).</summary>
    private static readonly Regex _emailRx = Rx(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");

    /// <summary>Compact detector for JWT‑looking tokens (header.payload.signature).</summary>
    private static readonly Regex _jwtRx = Rx(@"\beyJ[A-Za-z0-9_\-]+=*\.[A-Za-z0-9_\-]+=*\.[A-Za-z0-9_\-]+=*\b");

    // --- URL/path normalization helpers ---------------------------------------------

    /// <summary>Canonical lowercase hex GUID form (8-4-4-4-12).</summary>
    private static readonly Regex _guidRx = Rx(@"\b[0-9a-f]{8}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{12}\b", ignoreCase: false);

    /// <summary>Long integer segments (6+ digits) – often identifiers/order numbers.</summary>
    /// NOT: Negatif lookaround içerdiği için NonBacktracking otomatik kapanır.
    private static readonly Regex _numRx = Rx(@"(?<![A-Za-z0-9])\d{6,}(?![A-Za-z0-9])", ignoreCase: false);

    /// <summary>Base64url‑ish segments commonly found in opaque tokens.</summary>
    private static readonly Regex _b64uRx = Rx(@"^[A-Za-z0-9_\-]{16,}$", ignoreCase: false);

    // --- Public API ------------------------------------------------------------------

    /// <summary>
    /// Scrubs a single string value using the provided options: masks sensitive content,
    /// normalizes URL/path‑like shapes, and enforces a maximum length.
    /// </summary>
    public static string ScrubValue(string input, ScrubbingOptions? options = null)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var opt = options ?? ScrubbingOptions.Default;
        var value = input;

        if (opt.MaskSensitiveValues)
        {
            // Mask e‑mails
            value = _emailRx.Replace(value, opt.Mask);
            // Mask JWTs
            value = _jwtRx.Replace(value, opt.Mask);
        }

        if (opt.NormalizePaths)
        {
            value = NormalizeUrlOrPath(value, opt);
        }

        // Reduce cardinality: trim overly long values.
        var max = Math.Max(0, opt.MaxValueLength);
        if (value.Length > max)
            value = value[..max];

        return value;
    }

    /// <summary>
    /// Scrubs all string/URI values in the given <see cref="TagList"/> and optionally drops
    /// banned keys as defined by <see cref="ScrubbingOptions.BannedKeys"/>.
    /// </summary>
    public static void Apply(ref TagList tags, ScrubbingOptions? options = null)
    {
        var opt = options ?? ScrubbingOptions.Default;

        var newTags = new TagList();

        foreach (var tag in tags)
        {
            var key = tag.Key ?? string.Empty;
            var value = tag.Value;

            // Drop banned keys entirely if configured.
            if (opt.DropBannedKeys && opt.BannedKeys.Contains(key))
                continue;

            if (value is string s)
            {
                value = ScrubValue(s, opt);
            }
            else if (value is Uri uri)
            {
                // Convert to string and scrub with the same pipeline.
                value = ScrubValue(uri.ToString(), opt);
            }

            newTags.Add(key, value);
        }

        // Replace the incoming list with the scrubbed one.
        tags = newTags;
    }

    /// <summary>
    /// Normalizes URL- or path‑shaped inputs:
    /// <list type="bullet">
    ///   <item><description>Strips query string and fragment.</description></item>
    ///   <item><description>Masks e‑mail segments with <see cref="ScrubbingOptions.Mask"/>.</description></item>
    ///   <item><description>Replaces GUIDs and long numeric segments with <c>{id}</c>.</description></item>
    ///   <item><description>Replaces base64url‑like segments with <c>{tok}</c>.</description></item>
    /// </list>
    /// </summary>
    private static string NormalizeUrlOrPath(string input, ScrubbingOptions opt)
    {
        // Fast path: if there's no "/" and it doesn't start with "http", skip normalization.
        if (input.IndexOf('/') < 0 && !input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return input;

        string path;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            // Drop scheme/host/query/fragment — keep only the absolute path to reduce cardinality.
            path = uri.AbsolutePath;
        }
        else
        {
            // Manually strip query and fragment for non‑URI strings.
            var q = input.IndexOf('?');
            if (q >= 0)
                input = input[..q];
            var h = input.IndexOf('#');
            if (h >= 0)
                input = input[..h];
            path = input;
        }

        if (string.IsNullOrEmpty(path) || path == "/")
            return path;

        var embeddedB64uRx = Rx(@"[A-Za-z0-9_\-]{16,}", ignoreCase: false);

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var seg = parts[i];

            if (_emailRx.IsMatch(seg))
            {
                parts[i] = opt.Mask;
                continue;
            }

            var replaced = _guidRx.Replace(seg, "{id}");

            replaced = _numRx.Replace(replaced, "{id}");

            if (_b64uRx.IsMatch(replaced))
            {
                replaced = "{tok}";
            }
            else
            {
                replaced = embeddedB64uRx.Replace(replaced, "{tok}");
            }

            parts[i] = replaced;
        }

        return "/" + string.Join('/', parts);
    }

}

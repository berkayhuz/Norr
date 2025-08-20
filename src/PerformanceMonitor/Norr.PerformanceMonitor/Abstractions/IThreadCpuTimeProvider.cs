// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Provides access to the current thread's accumulated CPU time (user + kernel).
/// Implementations may rely on platform APIs and can be disabled on platforms
/// where the information is unavailable or not permitted by sandboxing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Intended use:</b> This abstraction is designed for low-overhead performance
/// monitoring, anomaly detection, and per-request diagnostics. It complements wall‑clock
/// measurements by exposing how much CPU time a thread actually consumed.
/// </para>
/// <para>
/// <b>Thread affinity:</b> All methods refer to the <em>calling</em> thread. Do not
/// cache a value across thread hops (e.g., when using <see cref="System.Threading.Tasks.Task"/>
/// continuations) unless you explicitly capture and restore thread context.
/// </para>
/// <para>
/// <b>Precision and overhead:</b> Resolution and cost vary by platform. Implementations
/// should prefer monotonic, kernel-provided counters when available. Consumers should
/// avoid calling this API in tight inner loops unless they have measured and accepted
/// the overhead on their target platform.
/// </para>
/// <para>
/// <b>Fallback behavior:</b> When the platform cannot provide CPU time, implementors
/// should return <see cref="TimeSpan.Zero"/> or throw only if documented; use
/// <see cref="IsSupported"/> to feature-detect at runtime. For cycle counts, prefer
/// <see cref="TryGetThreadCycleCount(out ulong)"/> which is explicitly best‑effort.
/// </para>
/// </remarks>
public interface IThreadCpuTimeProvider
{
    /// <summary>
    /// Gets a value indicating whether per‑thread CPU time is available on the current platform
    /// and permitted in the current process context.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if CPU time can be queried for the current thread; otherwise
    /// <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// Check this flag before calling <see cref="GetCurrentThreadCpuTime"/> to avoid
    /// platform‑specific failures or expensive no‑op calls. The value may depend on OS,
    /// container/sandbox configuration, or process privileges.
    /// </remarks>
    bool IsSupported
    {
        get;
    }

    /// <summary>
    /// Gets the total CPU time (user + kernel) consumed by the <em>current</em> thread
    /// since its start, if supported by the platform.
    /// </summary>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the accumulated CPU time of the current
    /// thread. Implementations should prefer a monotonic, high‑resolution clock when
    /// available.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned value is not wall‑clock time; it increases only while the thread
    /// is scheduled on a CPU. It may jump in coarse increments on platforms with low
    /// timer resolution.
    /// </para>
    /// <para>
    /// If <see cref="IsSupported"/> is <see langword="false"/>, implementations may
    /// return <see cref="TimeSpan.Zero"/> or throw <see cref="PlatformNotSupportedException"/>.
    /// Callers should feature‑detect via <see cref="IsSupported"/> and avoid exception‑driven flow.
    /// </para>
    /// </remarks>
    TimeSpan GetCurrentThreadCpuTime();

    /// <summary>
    /// Attempts to obtain the hardware cycle count for the <em>current</em> thread.
    /// </summary>
    /// <param name="cycles">
    /// When this method returns <see langword="true"/>, contains the monotonically increasing
    /// count of CPU cycles consumed by the current thread since its start. The unit is raw
    /// CPU cycles and is not normalized or converted to time.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the cycle count was retrieved; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Cycle counts are provided on a best‑effort basis (for example, via Windows
    /// thread cycle APIs when available). They are suitable for relative comparisons
    /// (e.g., “this request used twice as many cycles as that one”) but should not be
    /// converted to time without proper calibration (frequency scaling, turbo, and
    /// heterogeneous cores can invalidate naive conversions).
    /// </para>
    /// <para>
    /// Implementations must avoid throwing for unsupported platforms and should return
    /// <see langword="false"/> instead.
    /// </para>
    /// </remarks>
    bool TryGetThreadCycleCount(out ulong cycles);
}

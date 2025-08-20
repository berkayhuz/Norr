// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Runtime.InteropServices;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Core.Runtime;

/// <summary>
/// Provides per-thread CPU time and (optionally on Windows) CPU cycle counts in a
/// cross-platform manner.
/// </summary>
/// <remarks>
/// <para>
/// The provider attempts the most accurate and low-overhead mechanism per platform:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><b>Windows:</b> Uses <c>GetThreadTimes</c> for CPU time and <c>QueryThreadCycleTime</c>
///     for cycle counts. Both APIs operate on the <em>current</em> thread via <c>GetCurrentThread()</c>.
///     </description>
///   </item>
///   <item>
///     <description><b>Linux:</b> Uses <c>clock_gettime(CLOCK_THREAD_CPUTIME_ID)</c>.</description>
///   </item>
///   <item>
///     <description><b>macOS:</b> Attempts <c>clock_gettime(CLOCK_THREAD_CPUTIME_ID)</c> first; if unavailable,
///     falls back to Mach <c>thread_info(THREAD_BASIC_INFO)</c>.</description>
///   </item>
/// </list>
/// <para>
/// If none of the platform mechanisms are available, the provider reports
/// <see cref="IsSupported"/> as <see langword="false"/> and returns <see cref="TimeSpan.Zero"/>
/// from <see cref="GetCurrentThreadCpuTime"/>.
/// </para>
/// <para><b>Thread Safety:</b> The provider is stateless and thread-safe.</para>
/// </remarks>
public sealed class ThreadCpuTimeProvider : IThreadCpuTimeProvider
{
    private readonly bool _supported;

    /// <summary>
    /// Gets a value indicating whether the current platform supports querying per-thread CPU time
    /// (and, on Windows, CPU cycle counts).
    /// </summary>
    /// <remarks>
    /// When this property is <see langword="false"/>, <see cref="GetCurrentThreadCpuTime"/> returns
    /// <see cref="TimeSpan.Zero"/> and <see cref="TryGetThreadCycleCount(out ulong)"/> always returns
    /// <see langword="false"/>.
    /// </remarks>
    public bool IsSupported => _supported;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadCpuTimeProvider"/> class and probes platform
    /// support. The probe is lightweight and performed once per instance.
    /// </summary>
    public ThreadCpuTimeProvider() => _supported = ProbeSupport();

    /// <summary>
    /// Gets the amount of CPU time consumed by the <em>current thread</em>.
    /// </summary>
    /// <returns>
    /// The CPU time as a <see cref="TimeSpan"/> if supported; otherwise <see cref="TimeSpan.Zero"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned value reflects user+kernel time on Windows via <c>GetThreadTimes</c>,
    /// and thread CPU time via <c>clock_gettime(CLOCK_THREAD_CPUTIME_ID)</c> or Mach
    /// <c>thread_info(THREAD_BASIC_INFO)</c> on Unix-like systems.
    /// </para>
    /// <para>
    /// This method never throws on unsupported platforms; it returns <see cref="TimeSpan.Zero"/> instead.
    /// </para>
    /// </remarks>
    public TimeSpan GetCurrentThreadCpuTime()
    {
        if (!IsSupported)
            return TimeSpan.Zero;

        if (OperatingSystem.IsWindows())
        {
            if (!GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user))
                return TimeSpan.Zero;

            long userTicks = ((long)user.dwHighDateTime << 32) | (uint)user.dwLowDateTime;
            long kernelTicks = ((long)kernel.dwHighDateTime << 32) | (uint)kernel.dwLowDateTime;
            // FILETIME is 100-ns units; .NET ticks are also 100 ns.
            return new TimeSpan(userTicks + kernelTicks);
        }

        // POSIX: clock_gettime for the current thread
        if (ClockGetTime(CLOCK_THREAD_CPUTIME_ID, out var ts) == 0)
        {
            // Convert: seconds → ticks, nanoseconds → ticks (1 tick = 100 ns)
            long ticks = checked(ts.tv_sec * TimeSpan.TicksPerSecond + ts.tv_nsec / 100);
            return new TimeSpan(ticks);
        }

        // macOS fallback: Mach thread_info(THREAD_BASIC_INFO)
        if (OperatingSystem.IsMacOS())
        {
            var thread = pthread_mach_thread_np(pthread_self());
            var count = _tHREAD_BASIC_INFO_COUNT;
            var info = new thread_basic_info();
            int kr = thread_info(thread, THREAD_BASIC_INFO, ref info, ref count);
            if (kr == 0 /* KERN_SUCCESS */)
            {
                long usec =
                    (long)info.user_time.seconds * 1_000_000L + info.user_time.microseconds +
                    (long)info.system_time.seconds * 1_000_000L + info.system_time.microseconds;

                // microseconds → ticks (100 ns): * 10
                return new TimeSpan(checked(usec * 10));
            }
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Attempts to read the CPU cycle count consumed by the <em>current thread</em>.
    /// </summary>
    /// <param name="cycles">On success, receives the number of CPU cycles used by the current thread.</param>
    /// <returns>
    /// <see langword="true"/> on Windows when <c>QueryThreadCycleTime</c> succeeds; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This API is Windows-only. On non-Windows platforms, the method returns <see langword="false"/> and
    /// sets <paramref name="cycles"/> to <c>0</c>.
    /// </remarks>
    public bool TryGetThreadCycleCount(out ulong cycles)
    {
        cycles = 0UL;

        if (!OperatingSystem.IsWindows())
            return false;

        // QueryThreadCycleTime: number of CPU cycles consumed by the current thread.
        return QueryThreadCycleTime(GetCurrentThread(), out cycles);
    }

    /// <summary>
    /// Probes platform support for per-thread CPU time (and cycle counts on Windows).
    /// </summary>
    /// <returns><see langword="true"/> if supported on the current platform; otherwise <see langword="false"/>.</returns>
    private static bool ProbeSupport()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetThreadTimes(GetCurrentThread(), out _, out _, out _, out _);
            }

            if (OperatingSystem.IsLinux())
            {
                return ClockGetTime(CLOCK_THREAD_CPUTIME_ID, out _) == 0;
            }

            if (OperatingSystem.IsMacOS())
            {
                // Try clock_gettime first; if unavailable, try Mach fallback.
                if (ClockGetTime(CLOCK_THREAD_CPUTIME_ID, out _) == 0)
                    return true;

                var thread = pthread_mach_thread_np(pthread_self());
                var count = _tHREAD_BASIC_INFO_COUNT;
                var info = new thread_basic_info();
                return thread_info(thread, THREAD_BASIC_INFO, ref info, ref count) == 0; // KERN_SUCCESS
            }

            return false;
        }
        catch
        {
            // Any interop failure means "not supported".
            return false;
        }
    }

    // ---------------- Windows interop ----------------
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetThreadTimes(
        IntPtr hThread,
        out FILETIME creationTime,
        out FILETIME exitTime,
        out FILETIME kernelTime,
        out FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

    // ---------------- POSIX interop -------------------
    private const int CLOCK_THREAD_CPUTIME_ID = 3;

    // Use a PascalCase name to avoid CS8981 (all-lowercase type name).
    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int clock_gettime(int clk_id, out Timespec tp);

    private static int ClockGetTime(int clkId, out Timespec ts) => clock_gettime(clkId, out ts);

    // ---------------- macOS Mach fallback -------------
    private const int THREAD_BASIC_INFO = 3;
    private static readonly int _tHREAD_BASIC_INFO_COUNT = Marshal.SizeOf<thread_basic_info>() / sizeof(int);

    [StructLayout(LayoutKind.Sequential)]
    private struct time_value_t
    {
        public int seconds;
        public int microseconds;
    }

    // https://newosxbook.com/src.php?file=/osfmk/mach/thread_info.h
    [StructLayout(LayoutKind.Sequential)]
    private struct thread_basic_info
    {
        public time_value_t user_time;
        public time_value_t system_time;
        public int cpu_usage;
        public int policy;
        public int run_state;
        public int flags;
        public int suspend_count;
        public int sleep_time;
    }

    [DllImport("libSystem.B.dylib")]
    private static extern IntPtr pthread_self();

    [DllImport("libSystem.B.dylib")]
    private static extern uint pthread_mach_thread_np(IntPtr pthread);

    [DllImport("libSystem.B.dylib")]
    private static extern int thread_info(uint thread, int flavor, ref thread_basic_info threadInfo, ref int threadInfoCount);
}

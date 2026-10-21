using System.Runtime.InteropServices;

namespace CornerActions;

internal enum MonitorScope
{
    Primary,
    All,
}

/// <summary>
/// A desktop monitor in physical pixels, in virtual-screen coordinates.
/// </summary>
internal readonly record struct MonitorInfo(
    string Device,
    int Left,
    int Top,
    int Right,
    int Bottom,
    int Dpi,
    bool Primary)
{
    internal int Width => Right - Left;

    internal int Height => Bottom - Top;
}

internal static class MonitorLayout
{
    /// <summary>
    /// Enumerates the desktop monitors. Returns an empty list when the
    /// enumeration fails, which callers treat as "no hot corners".
    /// </summary>
    internal static List<MonitorInfo> Query()
    {
        var monitors = new List<MonitorInfo>();
        var size = Marshal.SizeOf<NativeMethods.MONITORINFOEX>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Enumerate, IntPtr.Zero);
        return monitors;

        bool Enumerate(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = size };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            monitors.Add(new MonitorInfo(
                info.szDevice ?? string.Empty,
                info.rcMonitor.Left,
                info.rcMonitor.Top,
                info.rcMonitor.Right,
                info.rcMonitor.Bottom,
                GetDpi(hMonitor),
                (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));

            return true;
        }
    }

    /// <summary>
    /// Filters the enumerated monitors by the configured scope. When the scope is
    /// <see cref="MonitorScope.Primary"/> and no monitor reports itself as primary,
    /// the first monitor is used so the feature never silently turns itself off.
    /// </summary>
    internal static List<MonitorInfo> Select(IReadOnlyList<MonitorInfo> all, MonitorScope scope)
    {
        if (scope == MonitorScope.All)
        {
            return [.. all];
        }

        foreach (var monitor in all)
        {
            if (monitor.Primary)
            {
                return [monitor];
            }
        }

        return all.Count > 0 ? [all[0]] : [];
    }

    /// <summary>
    /// Converts the configured corner size (device-independent pixels) into
    /// physical pixels for a specific monitor.
    /// </summary>
    internal static int CornerSizePx(MonitorInfo monitor, int cornerSizeDips)
    {
        var scaled = (int)Math.Round(cornerSizeDips * monitor.Dpi / 96.0, MidpointRounding.AwayFromZero);
        return Math.Max(2, scaled);
    }

    private static int GetDpi(IntPtr hMonitor)
    {
        // The .NET guidance is to prefer GetDpiForWindow for per-monitor aware
        // processes, but that API needs a window that already sits on the target
        // monitor. GetDpiForMonitor is the only direct per-monitor query; for a
        // PerMonitorV2 process it reports the real DPI of that monitor.
        if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            return (int)dpiX;
        }

        // 96 is the unscaled desktop DPI, so the corner size stays conservative.
        return 96;
    }
}

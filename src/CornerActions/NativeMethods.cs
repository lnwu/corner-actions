using System.Runtime.InteropServices;

namespace CornerActions;

/// <summary>
/// Win32 entry points used by the application. Every declaration here is used;
/// nothing is declared speculatively.
/// </summary>
internal static class NativeMethods
{
    internal const int WM_SYSCOMMAND = 0x0112;
    internal const int SC_MONITORPOWER = 0xF170;

    /// <summary>Send WM_SYSCOMMAND to every top-level window; DefWindowProc handles it.</summary>
    internal static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    internal const int VK_LBUTTON = 0x01;
    internal const int VK_RBUTTON = 0x02;
    internal const int VK_MBUTTON = 0x04;

    internal const uint MONITORINFOF_PRIMARY = 0x00000001;

    /// <summary>MDT_EFFECTIVE_DPI</summary>
    internal const int MDT_EFFECTIVE_DPI = 0;

    internal const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>SM_CXSMICON: the size of a small shell icon, DPI dependent.</summary>
    internal const int SM_CXSMICON = 49;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("shell32.dll")]
    internal static extern int SHQueryUserNotificationState(out int state);

    [DllImport("kernel32.dll")]
    internal static extern uint GetUserDefaultUILanguage();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);
}

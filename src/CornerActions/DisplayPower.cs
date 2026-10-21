using System.Runtime.InteropServices;

namespace CornerActions;

/// <summary>
/// Turns the display off. This is the documented way to run the system menu's
/// "turn off display" command: SC_MONITORPOWER with lParam 2, delivered to every
/// top-level window so that DefWindowProc performs it.
/// </summary>
internal static class DisplayPower
{
    private const uint TimeoutMilliseconds = 1000;

    /// <summary>
    /// Broadcasts the command. Returns false when no window accepted it within the
    /// timeout; the caller logs that instead of retrying in a loop.
    /// </summary>
    internal static bool TryTurnOff(out int errorCode)
    {
        var result = NativeMethods.SendMessageTimeout(
            NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SYSCOMMAND,
            new IntPtr(NativeMethods.SC_MONITORPOWER),
            new IntPtr(2),
            NativeMethods.SMTO_ABORTIFHUNG,
            TimeoutMilliseconds,
            out _);

        errorCode = result == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        return result != IntPtr.Zero;
    }
}

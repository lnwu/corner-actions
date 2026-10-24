using System.Runtime.InteropServices;

namespace CornerActions;

/// <summary>
/// Turns the display off. This is the documented way to run the system menu's
/// "turn off display" command: SC_MONITORPOWER with lParam 2, handled by
/// DefWindowProc. It goes to the app's own hidden window, not HWND_BROADCAST, so
/// no other application receives the command and a slow one cannot stall us.
/// </summary>
internal static class DisplayPower
{
    private const uint TimeoutMilliseconds = 1000;

    /// <summary>
    /// Sends the command to <paramref name="window"/>. Returns false when the call
    /// failed; the caller logs that instead of retrying in a loop.
    /// </summary>
    internal static bool TryTurnOff(IntPtr window, out int errorCode)
    {
        var result = NativeMethods.SendMessageTimeout(
            window,
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

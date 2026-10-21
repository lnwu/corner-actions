namespace CornerActions;

/// <summary>
/// Decides whether the current desktop state makes an action inappropriate.
/// </summary>
internal static class SessionState
{
    // QUERY_USER_NOTIFICATION_STATE values that mean "do not interrupt".
    private const int QunsUndefined = 0;
    private const int QunsNotPresent = 1;        // locked, UAC secure desktop, screensaver
    private const int QunsRunningD3DFullScreen = 3;
    private const int QunsPresentationMode = 4;

    /// <summary>
    /// True when the session is locked, a secure desktop is up, a presentation is
    /// running, or an exclusive Direct3D app owns the screen.
    ///
    /// QUNS_BUSY (2) is deliberately not in this list even though it also covers
    /// full-screen windows: a maximised video player or a full-screen browser is
    /// exactly where a user reaches for a corner to kill the display. When the
    /// query fails the action is allowed, so an unknown shell state cannot disable
    /// the feature forever.
    /// </summary>
    internal static bool ShouldSkipAction()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }

        return state is QunsUndefined or QunsNotPresent
            or QunsRunningD3DFullScreen or QunsPresentationMode;
    }

    /// <summary>
    /// True while any mouse button is held. Dragging a window towards a corner, or
    /// selecting text there, must not turn the display off.
    /// </summary>
    internal static bool AnyMouseButtonDown()
    {
        return IsDown(NativeMethods.VK_LBUTTON)
            || IsDown(NativeMethods.VK_RBUTTON)
            || IsDown(NativeMethods.VK_MBUTTON);

        static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}

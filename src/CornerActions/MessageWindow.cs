namespace CornerActions;

/// <summary>
/// A hidden window that receives messages the tray icon cannot: the shell's
/// broadcast "TaskbarCreated" message, which also arrives when the primary
/// display DPI changes, and the note a second copy of the app leaves on exit.
/// </summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private const string AlreadyRunningMessage = "CornerActions.AlreadyRunning";

    private readonly uint _taskbarCreated;
    private readonly uint _alreadyRunning;

    internal MessageWindow()
    {
        _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        _alreadyRunning = NativeMethods.RegisterWindowMessage(AlreadyRunningMessage);
        CreateHandle(new CreateParams
        {
            Caption = "CornerActions.MessageWindow",
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0,
        });
    }

    internal event Action? TaskbarCreated;

    /// <summary>Raised when another copy of the app started and exited.</summary>
    internal event Action? AlreadyRunning;

    /// <summary>
    /// Tells the running copy that the user tried to start another one. Posted,
    /// not sent, so the exiting copy never waits on other windows.
    /// </summary>
    internal static void NotifyRunningInstance()
    {
        var message = NativeMethods.RegisterWindowMessage(AlreadyRunningMessage);
        if (message != 0)
        {
            NativeMethods.PostMessageW(NativeMethods.HWND_BROADCAST, (int)message, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void Dispose() => DestroyHandle();

    protected override void WndProc(ref Message m)
    {
        if (_taskbarCreated != 0 && m.Msg == (int)_taskbarCreated)
        {
            TaskbarCreated?.Invoke();
        }
        else if (_alreadyRunning != 0 && m.Msg == (int)_alreadyRunning)
        {
            AlreadyRunning?.Invoke();
        }

        base.WndProc(ref m);
    }
}

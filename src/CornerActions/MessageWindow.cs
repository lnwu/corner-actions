namespace CornerActions;

/// <summary>
/// A hidden window that receives messages the tray icon cannot: the shell's
/// broadcast "TaskbarCreated" message, which also arrives when the primary
/// display DPI changes.
/// </summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private readonly uint _taskbarCreated;

    internal MessageWindow()
    {
        _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
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

    public void Dispose() => DestroyHandle();

    protected override void WndProc(ref Message m)
    {
        if (_taskbarCreated != 0 && m.Msg == (int)_taskbarCreated)
        {
            TaskbarCreated?.Invoke();
        }

        base.WndProc(ref m);
    }
}

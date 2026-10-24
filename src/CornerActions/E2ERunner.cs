namespace CornerActions;

/// <summary>
/// End-to-end check of the real tray app in dry-run mode: park the pointer in the
/// primary monitor's top-left corner, wait for the dwell to elapse, and require
/// exactly one trigger. The pointer is restored afterwards and the display is
/// never switched off.
/// </summary>
internal static class E2ERunner
{
    private const int MoveDelayMs = 100;
    private const int RunDurationMs = 2500;

    internal static int Run(TrayAppContext context)
    {
        if (context.ActiveMonitors.Count == 0)
        {
            return Fail("no monitors were enumerated");
        }

        var monitor = context.ActiveMonitors[0];
        if (!NativeMethods.GetCursorPos(out var saved))
        {
            return Fail("GetCursorPos failed before the test");
        }

        // Report why the app might refuse to act, so a suppressed run is not
        // mistaken for a detection bug.
        NativeMethods.SHQueryUserNotificationState(out var notificationState);
        Log.Info($"e2e start monitor=\"{monitor.Device}\" bounds={monitor.Left},{monitor.Top}-{monitor.Right},{monitor.Bottom} dpi={monitor.Dpi} quns={notificationState}");

        // Park the pointer on every tick instead of once: a real mouse drifting by
        // a few pixels would otherwise leave the corner zone mid-test.
        using var mover = new System.Windows.Forms.Timer { Interval = MoveDelayMs };
        mover.Tick += (_, _) =>
        {
            if (!NativeMethods.SetCursorPos(monitor.Left + 1, monitor.Top + 1))
            {
                Log.Warn($"e2e set-cursor-pos failed win32={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            }
        };
        mover.Start();

        using var stopper = new System.Windows.Forms.Timer { Interval = RunDurationMs };
        stopper.Tick += (_, _) =>
        {
            mover.Stop();
            Application.ExitThread();
        };
        stopper.Start();

        Application.Run(context);

        NativeMethods.SetCursorPos(saved.X, saved.Y);

        var triggers = context.TriggerCount;
        var message = $"e2e {(triggers == 1 ? "PASS" : "FAIL")} triggers={triggers} expected=1";
        Log.Info(message);
        Console.WriteLine(message);
        return triggers == 1 ? 0 : 1;
    }

    internal static int Fail(string reason)
    {
        var message = $"e2e FAIL reason=\"{reason}\"";
        Log.Error(message);
        Console.WriteLine(message);
        return 1;
    }
}

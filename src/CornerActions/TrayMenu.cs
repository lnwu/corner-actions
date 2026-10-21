namespace CornerActions;

internal enum TrayCommand
{
    None = 0,
    ToggleEnabled = 1,
    ToggleTopLeft = 2,
    ToggleTopRight = 3,
    ToggleAutoStart = 4,
    OpenConfig = 5,
    ReloadConfig = 6,
    Exit = 7,
}

/// <summary>
/// The tray context menu, built as a real Win32 popup menu instead of a
/// <see cref="ContextMenuStrip"/>. That is the menu Explorer itself uses for tray
/// icons, so it gets the system font, metrics, selection colours and, on Windows
/// 11, rounded corners. A ToolStrip draws its own Office-era chrome and never
/// matches the shell.
/// </summary>
internal static class TrayMenu
{
    /// <summary>Number of items the menu always contains, separators included.</summary>
    internal const int ItemCount = 11;

    /// <summary>
    /// Builds the menu. Callers own the returned handle and must destroy it.
    /// </summary>
    internal static IntPtr Build(AppConfig config, Strings strings, bool autoStartEnabled)
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            Log.Warn("menu creation failed");
            return IntPtr.Zero;
        }

        AddItem(menu, TrayCommand.ToggleEnabled, strings.MenuEnabled, config.Enabled);
        AddSeparator(menu);
        AddItem(menu, TrayCommand.ToggleTopLeft, strings.MenuTopLeft, config.TopLeft);
        AddItem(menu, TrayCommand.ToggleTopRight, strings.MenuTopRight, config.TopRight);
        AddSeparator(menu);
        AddItem(menu, TrayCommand.ToggleAutoStart, strings.MenuStartWithWindows, autoStartEnabled);
        AddSeparator(menu);
        AddItem(menu, TrayCommand.OpenConfig, strings.MenuOpenConfig, false);
        AddItem(menu, TrayCommand.ReloadConfig, strings.MenuReloadConfig, false);
        AddSeparator(menu);
        AddItem(menu, TrayCommand.Exit, strings.MenuExit, false);

        return menu;
    }

    /// <summary>
    /// Shows the menu at the cursor and returns the chosen command. The call blocks
    /// until the menu closes, which is why the owner is put in the foreground first:
    /// without it the menu ignores clicks outside itself.
    /// </summary>
    internal static TrayCommand Show(IntPtr owner, AppConfig config, Strings strings, bool autoStartEnabled)
    {
        var menu = Build(config, strings, autoStartEnabled);
        if (menu == IntPtr.Zero)
        {
            return TrayCommand.None;
        }

        try
        {
            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return TrayCommand.None;
            }

            NativeMethods.SetForegroundWindow(owner);

            var command = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                cursor.X,
                cursor.Y,
                owner,
                IntPtr.Zero);

            // Documented workaround: the shell needs a message posted to the owner
            // after the menu closes, or the next click lands on the menu instead of
            // the window under the cursor.
            NativeMethods.PostMessageW(owner, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            return (TrayCommand)command;
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private static void AddItem(IntPtr menu, TrayCommand command, string text, bool isChecked)
    {
        var flags = NativeMethods.MF_STRING;
        if (isChecked)
        {
            flags |= NativeMethods.MF_CHECKED;
        }

        NativeMethods.AppendMenuW(menu, flags, new IntPtr((int)command), text);
    }

    private static void AddSeparator(IntPtr menu) =>
        NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
}

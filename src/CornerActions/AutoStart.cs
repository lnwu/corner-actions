using Microsoft.Win32;

namespace CornerActions;

/// <summary>
/// Registers the app under HKCU\...\Run so it starts at logon. Per-user, no
/// elevation, and the value is only written when the user toggles the menu item.
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CornerActions";

    /// <summary>
    /// The Run value this executable would write. Comparing against it means a
    /// leftover entry from a moved executable shows up as "not enabled" instead of
    /// claiming a startup entry that starts something else.
    /// </summary>
    private static string CommandLine
    {
        get
        {
            var executable = Environment.ProcessPath;
            return string.IsNullOrEmpty(executable) ? string.Empty : $"\"{executable}\"";
        }
    }

    internal static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                var value = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(value)
                    && string.Equals(value, CommandLine, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                Log.Warn($"autostart read failed error=\"{ex.GetType().Name}: {ex.Message}\"");
                return false;
            }
        }
    }

    /// <summary>
    /// Adds or removes the Run entry. The executable path is quoted because it may
    /// contain spaces; the Run value must stay under 260 characters.
    /// </summary>
    internal static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                Log.Warn("autostart write failed reason=key-unavailable");
                return;
            }

            if (enabled)
            {
                var commandLine = CommandLine;
                if (commandLine.Length == 0)
                {
                    Log.Warn("autostart write failed reason=unknown-executable-path");
                    return;
                }

                key.SetValue(ValueName, commandLine, RegistryValueKind.String);
                Log.Info($"autostart enabled command=\"{commandLine}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Info("autostart disabled");
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Log.Warn($"autostart write failed error=\"{ex.GetType().Name}: {ex.Message}\"");
        }
    }
}

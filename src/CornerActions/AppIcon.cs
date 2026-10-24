namespace CornerActions;

/// <summary>
/// The application mark: a night-sky tile with amber brackets in the top corners
/// and a crescent moon, the display going to sleep. Both the executable icon and
/// the tray icon come from <c>assets/app.ico</c>, which is embedded at build time,
/// so there is exactly one copy of the artwork.
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "CornerActions.app.ico";

    /// <summary>
    /// Loads the frame that matches the shell's small-icon size, so the tray icon
    /// stays crisp on a scaled display instead of being resampled from 32 px.
    /// </summary>
    internal static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"embedded resource {ResourceName} is missing");

        var size = Math.Max(16, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON));
        return new Icon(stream, new Size(size, size));
    }
}

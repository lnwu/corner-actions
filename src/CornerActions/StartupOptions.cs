namespace CornerActions;

/// <summary>Command-line switches. Everything beyond these starts the tray app.</summary>
internal sealed class StartupOptions
{
    internal const string Usage = """
        Corner Actions - hot corners for Windows

          (no switch)  Run the tray application
          --selftest   Run the built-in detection checks and exit (0 = pass)
          --e2e        Dry-run the real app: park the pointer in the primary
                       top-left corner, expect exactly one trigger, then exit
          --dry-run    Detect corners and log them, never turn the display off
          --verbose    Echo log lines to the console
          --help       Show this text
        """;

    private StartupOptions()
    {
    }

    internal bool SelfTest { get; private set; }

    internal bool EndToEnd { get; private set; }

    internal bool DryRun { get; private set; }

    internal bool Verbose { get; private set; }

    internal bool Help { get; private set; }

    internal static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();

        foreach (var argument in args)
        {
            switch (argument.Trim().ToLowerInvariant())
            {
                case "--selftest":
                    options.SelfTest = true;
                    break;
                case "--e2e":
                    options.EndToEnd = true;
                    options.DryRun = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    options.Help = true;
                    break;
                default:
                    // Unknown switches are ignored on purpose: a stale shortcut
                    // should still start the tray app instead of failing.
                    break;
            }
        }

        return options;
    }
}

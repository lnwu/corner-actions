namespace CornerActions;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = StartupOptions.Parse(args);

        if (options.SelfTest || options.EndToEnd || options.Help || options.Verbose)
        {
            AttachConsole();
            Log.EchoToConsole = true;
        }

        if (options.Help)
        {
            Console.WriteLine(StartupOptions.Usage);
            return 0;
        }

        if (options.SelfTest)
        {
            return SelfTest.Run();
        }

        using var singleInstance = new SingleInstance();
        if (!singleInstance.IsFirst)
        {
            Log.Info("second instance exited");
            return 0;
        }

        // Applies ApplicationHighDpiMode=PerMonitorV2 from the project file.
        ApplicationConfiguration.Initialize();

        var config = AppConfig.Load();

        if (options.EndToEnd)
        {
            // The test must not depend on the user's current settings.
            config.Enabled = true;
            config.Monitors = "primary";
            config.TopLeft = true;
            config.CornerSizeDips = Math.Max(config.CornerSizeDips, AppConfig.DefaultCornerSizeDips);
        }

        var context = new TrayAppContext(config, options);

        if (options.EndToEnd)
        {
            return E2ERunner.Run(context);
        }

        Application.Run(context);
        return 0;
    }

    /// <summary>
    /// A WinExe has no console of its own. Attaching to the launching console makes
    /// --selftest/--e2e/--help usable from a terminal; everywhere else the log file
    /// carries the same information.
    /// </summary>
    private static void AttachConsole()
    {
        if (!NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
        {
            return;
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }
}

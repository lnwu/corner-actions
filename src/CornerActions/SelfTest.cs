namespace CornerActions;

/// <summary>
/// Built-in checks for the corner logic and the embedded artwork. Needs no desktop
/// session and no input devices, and returns 0 only when every check passes.
/// </summary>
internal static class SelfTest
{
    private static int _failures;
    private static int _checks;

    internal static int Run()
    {
        Console.WriteLine("corner-actions selftest");

        // A 1920x1080 primary display at 100% scaling.
        MonitorInfo[] primary = [Monitor(0, 0, 1920, 1080)];
        var m = primary[0];

        // Entering the corner is not enough; the pointer has to stay for 300 ms.
        var detector = NewDetector();
        Check("entry alone does not fire", detector.Evaluate(primary, true, true, 2, 2, 0) == HotCorner.None);
        Check("dwell not yet elapsed", detector.Evaluate(primary, true, true, 2, 2, 299) == HotCorner.None);
        Check("fires after 300 ms of dwell", detector.Evaluate(primary, true, true, 2, 2, 300) == HotCorner.TopLeft);

        // The zone is 4 device-independent pixels wide at 100% scaling.
        detector = NewDetector();
        Check("4 px is outside the zone", detector.Evaluate(primary, true, true, 4, 0, 0) == HotCorner.None);
        Check("still outside after a second sample",
            detector.Evaluate(primary, true, true, 4, 0, 5000) == HotCorner.None);

        detector = NewDetector();
        Check("3 px is inside the zone", detector.Evaluate(primary, true, true, 3, 0, 0) == HotCorner.None);
        Check("3 px fires after dwell", detector.Evaluate(primary, true, true, 3, 0, 300) == HotCorner.TopLeft);

        detector = NewDetector();
        Check("below the corner is not a hit", detector.Evaluate(primary, true, true, 0, 4, 0) == HotCorner.None);

        // A parked pointer must never fire twice.
        detector = NewDetector();
        detector.Evaluate(primary, true, true, 2, 2, 0);
        Check("first fire", detector.Evaluate(primary, true, true, 2, 2, 300) == HotCorner.TopLeft);
        Check("no repeat while parked", detector.Evaluate(primary, true, true, 2, 2, 1000) == HotCorner.None);
        Check("still no repeat later", detector.Evaluate(primary, true, true, 2, 2, 5000) == HotCorner.None);

        // Re-arming requires leaving the corner and the 2 s cooldown.
        detector = NewDetector();
        detector.Evaluate(primary, true, true, 2, 2, 0);
        detector.Evaluate(primary, true, true, 2, 2, 300);
        Check("leaving does not fire", detector.Evaluate(primary, true, true, 200, 200, 400) == HotCorner.None);
        Check("re-entry starts a new dwell", detector.Evaluate(primary, true, true, 2, 2, 500) == HotCorner.None);
        Check("cooldown blocks a quick re-entry",
            detector.Evaluate(primary, true, true, 2, 2, 800) == HotCorner.None);
        Check("cooldown still blocking just before it expires",
            detector.Evaluate(primary, true, true, 2, 2, 2299) == HotCorner.None);
        Check("fires after cooldown and dwell", detector.Evaluate(primary, true, true, 2, 2, 2300) == HotCorner.TopLeft);

        // Hysteresis: 4 extra pixels are required before the corner re-arms.
        detector = NewDetector();
        detector.Evaluate(primary, true, true, 2, 2, 0);
        detector.Evaluate(primary, true, true, 2, 2, 300);
        Check("inside hysteresis margin stays latched",
            detector.Evaluate(primary, true, true, 7, 0, 700) == HotCorner.None);
        Check("outside hysteresis margin re-arms",
            detector.Evaluate(primary, true, true, 8, 0, 700) == HotCorner.None);
        Check("back inside after re-arm starts a new dwell",
            detector.Evaluate(primary, true, true, 2, 2, 900) == HotCorner.None);
        Check("re-armed corner fires after cooldown",
            detector.Evaluate(primary, true, true, 2, 2, 2400) == HotCorner.TopLeft);

        // Leaving resets the dwell timer instead of accumulating it.
        detector = NewDetector();
        detector.Evaluate(primary, true, true, 2, 2, 0);
        detector.Evaluate(primary, true, true, 200, 200, 200);
        detector.Evaluate(primary, true, true, 2, 2, 250);
        Check("dwell restarted after leaving",
            detector.Evaluate(primary, true, true, 2, 2, 400) == HotCorner.None);
        Check("dwell completes after the restart",
            detector.Evaluate(primary, true, true, 2, 2, 600) == HotCorner.TopLeft);

        // Disabled corners stay silent.
        detector = NewDetector();
        Check("disabled top-left never fires",
            detector.Evaluate(primary, false, true, 2, 2, 0) == HotCorner.None &&
            detector.Evaluate(primary, false, true, 2, 2, 5000) == HotCorner.None);

        // Top-right is measured from the right edge of the monitor.
        detector = NewDetector();
        Check("top-right inside", detector.Evaluate(primary, true, true, 1919, 0, 0) == HotCorner.None);
        Check("top-right fires", detector.Evaluate(primary, true, true, 1919, 0, 300) == HotCorner.TopRight);

        // Multi-monitor scope.
        MonitorInfo[] all =
        [
            Monitor(0, 0, 1920, 1080),
            Monitor(1920, 0, 2560, 1440, dpi: 144, primary: false),
        ];

        var primaryOnly = MonitorLayout.Select(all, MonitorScope.Primary);
        detector = NewDetector();
        Check("primary scope has one monitor", primaryOnly.Count == 1);
        Check("primary scope ignores the second monitor corner",
            detector.Evaluate(primaryOnly, true, true, 1921, 1, 5000) == HotCorner.None);

        var everyMonitor = MonitorLayout.Select(all, MonitorScope.All);
        detector = NewDetector();
        Check("all scope has two monitors", everyMonitor.Count == 2);
        Check("all scope entry", detector.Evaluate(everyMonitor, true, true, 1921, 1, 0) == HotCorner.None);
        Check("all scope fires", detector.Evaluate(everyMonitor, true, true, 1921, 1, 300) == HotCorner.TopLeft);

        // 4 DIP at 150% scaling is 6 physical pixels.
        detector = NewDetector();
        Check("150% zone includes 5 px", detector.Evaluate(everyMonitor, true, true, 1925, 1, 0) == HotCorner.None);
        Check("150% zone fires", detector.Evaluate(everyMonitor, true, true, 1925, 1, 300) == HotCorner.TopLeft);

        detector = NewDetector();
        Check("150% zone excludes 6 px", detector.Evaluate(everyMonitor, true, true, 1926, 1, 5000) == HotCorner.None);

        // Negative coordinates, as used by a monitor placed left of the primary one.
        MonitorInfo[] leftOfPrimary = [Monitor(-1920, 0, 1920, 1080, primary: false)];
        detector = NewDetector();
        Check("negative coordinates entry",
            detector.Evaluate(leftOfPrimary, true, true, -1919, 1, 0) == HotCorner.None);
        Check("negative coordinates fires",
            detector.Evaluate(leftOfPrimary, true, true, -1919, 1, 300) == HotCorner.TopLeft);

        // UI language: Chinese for any Chinese locale, English otherwise.
        Check("zh-CN id is Chinese", Strings.IsChineseUiLanguage(0x0804));
        Check("zh-TW id is Chinese", Strings.IsChineseUiLanguage(0x0404));
        Check("en-US id is not Chinese", !Strings.IsChineseUiLanguage(0x0409));
        var zhStrings = new Strings("zh");
        var enStrings = new Strings("en");
        Check("forced zh resolves to zh", zhStrings.Code == "zh" && enStrings.Code == "en");
        Check("forced languages have different menu text",
            zhStrings.MenuEnabled != enStrings.MenuEnabled);

        // The tray icon is loaded from the embedded copy of assets/app.ico; a wrong
        // LogicalName would only show up as a missing icon at runtime.
        using (var icon = AppIcon.Load())
        {
            Check("embedded app icon loads", icon.Width >= 16 && icon.Height >= 16);
        }

        // Corner size never collapses to zero, even for a 1 DIP configuration.
        Check("corner size clamps to 2 px", MonitorLayout.CornerSizePx(m, 1) == 2);

        var summary = $"selftest {(_failures == 0 ? "PASS" : "FAIL")} checks={_checks} failures={_failures}";
        Console.WriteLine(summary);
        Log.Info(summary);
        return _failures == 0 ? 0 : 1;
    }

    private static CornerDetector NewDetector() => new(cornerSizeDips: 4, dwellMs: 300, cooldownMs: 2000);

    private static MonitorInfo Monitor(int left, int top, int width, int height, int dpi = 96, bool primary = true) =>
        new(
            primary ? @"\\.\DISPLAY1" : @"\\.\DISPLAY2",
            left,
            top,
            left + width,
            top + height,
            dpi,
            primary);

    private static void Check(string name, bool condition)
    {
        _checks++;
        if (condition)
        {
            Console.WriteLine($"ok   {name}");
            return;
        }

        _failures++;
        Console.WriteLine($"FAIL {name}");
        Log.Error($"selftest check failed name=\"{name}\"");
    }
}

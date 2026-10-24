using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CornerActions;

/// <summary>
/// The tray-only application. Owns the notify icon, the polling timer, the monitor
/// cache and the corner detector; there is no main window.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    /// <summary>
    /// Cursor polling interval. 50 ms keeps the dwell measurement within one tick
    /// while calling GetCursorPos at a rate that costs nothing measurable.
    /// </summary>
    private const int PollIntervalMs = 50;

    private const int SessionCheckIntervalMs = 250;

    /// <summary>
    /// While the session is locked, presenting, or an exclusive full-screen app is
    /// in front, the feature cannot fire anyway, so the poll slows to 1 Hz. That
    /// keeps a full-screen game from being woken 20 times a second by a tray app.
    /// </summary>
    private const int SuppressedPollIntervalMs = 1000;
    private const int LayoutRetryIntervalMs = 2000;
    private const int CursorErrorLogIntervalMs = 60_000;

    /// <summary>
    /// Fixed rather than configurable: after the display turns off, the next mouse
    /// movement turns it back on, and a second trigger during that window would
    /// look like the feature is fighting the user.
    /// </summary>
    private const int CooldownMilliseconds = 2000;

    private readonly StartupOptions _options;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly MessageWindow _messageWindow;
    private readonly Icon _trayIcon;

    private AppConfig _config;
    private Strings _strings;
    private CornerDetector _detector;
    private List<MonitorInfo> _monitors = [];

    private long _lastSessionCheckMs;
    private long _lastLayoutAttemptMs;
    private long _lastCursorErrorLogMs;
    private bool _sessionSuppressed;
    private bool _suppressionLogged;
    private int _lastLoggedX = int.MinValue;
    private int _lastLoggedY = int.MinValue;
    private volatile bool _layoutDirty;
    private int _triggerCount;
    private bool _disposed;

    internal TrayAppContext(AppConfig config, StartupOptions options)
    {
        _config = config;
        _options = options;
        _strings = new Strings(config);
        _detector = CreateDetector(config);

        _trayIcon = AppIcon.Load();
        _icon = new NotifyIcon
        {
            Icon = _trayIcon,
            Visible = true,
            Text = _strings.TooltipEnabled,
        };

        _icon.MouseUp += OnIconMouseUp;
        _icon.DoubleClick += (_, _) => ToggleEnabled();

        _messageWindow = new MessageWindow();
        _messageWindow.TaskbarCreated += OnTaskbarCreated;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _timer.Tick += OnTick;

        RebuildLayout("startup");
        ApplyEnabled();

        Log.Info(
            $"started enabled={_config.Enabled} scope={_config.Monitors} " +
            $"cornerSizeDips={_config.CornerSizeDips} dwellMs={_config.DwellMilliseconds} " +
            $"topLeft={_config.TopLeft} topRight={_config.TopRight} " +
            $"mode={(_options.DryRun ? "dry-run" : "live")} lang={_strings.Code} monitors={_monitors.Count}");
    }

    /// <summary>Raised for every action, after the display command was attempted.</summary>
    internal event Action<HotCorner>? Triggered;

    internal IReadOnlyList<MonitorInfo> ActiveMonitors => _monitors;

    internal int TriggerCount => Volatile.Read(ref _triggerCount);

    protected override void ExitThreadCore()
    {
        if (!_disposed)
        {
            _disposed = true;

            _timer.Stop();
            _timer.Dispose();

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _messageWindow.TaskbarCreated -= OnTaskbarCreated;
            _messageWindow.Dispose();

            _icon.Visible = false;
            _icon.Dispose();
            _trayIcon.Dispose();

            Log.Info($"stopped triggers={_triggerCount}");
        }

        base.ExitThreadCore();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_layoutDirty)
        {
            RebuildLayout("display-changed");
        }

        if (_monitors.Count == 0)
        {
            if (Environment.TickCount64 - _lastLayoutAttemptMs >= LayoutRetryIntervalMs)
            {
                RebuildLayout("retry");
            }

            return;
        }

        var now = Environment.TickCount64;

        if (now - _lastSessionCheckMs >= SessionCheckIntervalMs)
        {
            _lastSessionCheckMs = now;

            var suppressed = SessionState.ShouldSkipAction();
            if (suppressed != _sessionSuppressed)
            {
                _sessionSuppressed = suppressed;
                _timer.Interval = suppressed ? SuppressedPollIntervalMs : PollIntervalMs;
                Log.Info($"session suppressed={suppressed} pollMs={_timer.Interval}");
            }
        }

        var mouseDown = SessionState.AnyMouseButtonDown();
        if (_sessionSuppressed || mouseDown)
        {
            if (_options.Verbose && !_suppressionLogged)
            {
                _suppressionLogged = true;
                Log.Info($"suppressed session={_sessionSuppressed} mouseDown={mouseDown}");
            }

            _detector.Reset();
            return;
        }

        _suppressionLogged = false;

        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            // Locked or non-input desktops cannot be queried. Log rarely: this can
            // repeat on every tick for as long as the session stays unavailable.
            if (now - _lastCursorErrorLogMs >= CursorErrorLogIntervalMs)
            {
                _lastCursorErrorLogMs = now;
                Log.Warn($"cursor query failed win32={Marshal.GetLastWin32Error()}");
            }

            return;
        }

        var corner = _detector.Evaluate(
            _monitors,
            _config.TopLeft,
            _config.TopRight,
            cursor.X,
            cursor.Y,
            now);

        if (_options.Verbose && (cursor.X != _lastLoggedX || cursor.Y != _lastLoggedY))
        {
            _lastLoggedX = cursor.X;
            _lastLoggedY = cursor.Y;
            Log.Info($"cursor x={cursor.X} y={cursor.Y} corner={Describe(corner)}");
        }

        if (corner != HotCorner.None)
        {
            Fire(corner);
        }
    }

    private void Fire(HotCorner corner)
    {
        Interlocked.Increment(ref _triggerCount);

        if (_options.DryRun)
        {
            Log.Info($"trigger corner={Describe(corner)} action=monitor-off mode=dry-run");
        }
        else if (DisplayPower.TryTurnOff(out var errorCode))
        {
            Log.Info($"trigger corner={Describe(corner)} action=monitor-off result=ok");
        }
        else
        {
            Log.Warn($"trigger corner={Describe(corner)} action=monitor-off result=failed win32={errorCode}");
        }

        Triggered?.Invoke(corner);
    }

    private void OnTaskbarCreated()
    {
        // The shell restarted, or the primary display DPI changed. Both invalidate
        // the tray icon and, in the DPI case, the pixel geometry of the corners.
        Log.Info("taskbar-created");
        _icon.Visible = false;
        _icon.Visible = true;
        RebuildLayout("taskbar-created");
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Raised on a SystemEvents thread, so only flag the work here.
        _layoutDirty = true;
    }

    private void RebuildLayout(string reason)
    {
        _layoutDirty = false;
        _lastLayoutAttemptMs = Environment.TickCount64;

        _monitors = MonitorLayout.Select(MonitorLayout.Query(), _config.Scope);
        _detector.Reset();

        if (reason != "retry")
        {
            Log.Info($"layout reason={reason} scope={_config.Monitors} monitors={_monitors.Count}");
        }
    }

    private void ToggleEnabled()
    {
        _config.Enabled = !_config.Enabled;
        _config.Save();
        _detector.Reset();
        ApplyEnabled();
        Log.Info($"toggled enabled={_config.Enabled}");
    }

    /// <summary>
    /// Shows the enabled state in the tooltip and stops polling while disabled, so
    /// a switched-off app costs no timer ticks at all.
    /// </summary>
    private void ApplyEnabled()
    {
        _icon.Text = _config.Enabled ? _strings.TooltipEnabled : _strings.TooltipDisabled;
        _timer.Enabled = _config.Enabled;
    }

    private void OnIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var command = TrayMenu.Show(_messageWindow.Handle, _config, _strings, AutoStart.IsEnabled);
        switch (command)
        {
            case TrayCommand.ToggleEnabled:
                ToggleEnabled();
                break;
            case TrayCommand.ToggleTopLeft:
                ToggleCorner(HotCorner.TopLeft);
                break;
            case TrayCommand.ToggleTopRight:
                ToggleCorner(HotCorner.TopRight);
                break;
            case TrayCommand.ToggleAutoStart:
                AutoStart.Set(!AutoStart.IsEnabled);
                break;
            case TrayCommand.OpenConfig:
                OpenConfigFile();
                break;
            case TrayCommand.ReloadConfig:
                ReloadConfig();
                break;
            case TrayCommand.Exit:
                ExitThread();
                break;
        }
    }

    private void ToggleCorner(HotCorner corner)
    {
        if (corner == HotCorner.TopLeft)
        {
            _config.TopLeft = !_config.TopLeft;
        }
        else
        {
            _config.TopRight = !_config.TopRight;
        }

        _config.Save();
        _detector.Reset();
        Log.Info($"corner toggled corner={Describe(corner)} enabled={(corner == HotCorner.TopLeft ? _config.TopLeft : _config.TopRight)}");
    }

    private void ReloadConfig()
    {
        var reloaded = AppConfig.Load();
        if (reloaded.LoadFailed)
        {
            MessageBox.Show(_strings.ConfigInvalid, _strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config = reloaded;
        _strings = new Strings(_config);
        _detector = CreateDetector(_config);
        RebuildLayout("config-reload");
        ApplyEnabled();
        Log.Info($"config reloaded enabled={_config.Enabled} language={_config.Language}");
    }

    private void OpenConfigFile()
    {
        try
        {
            if (!File.Exists(AppConfig.FilePath))
            {
                _config.Save();
            }

            Process.Start(new ProcessStartInfo(AppConfig.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log.Error($"config open failed error=\"{ex.GetType().Name}: {ex.Message}\"");
            MessageBox.Show(_strings.ConfigUnavailable, _strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static CornerDetector CreateDetector(AppConfig config) =>
        new(config.CornerSizeDips, config.DwellMilliseconds, CooldownMilliseconds);

    private static string Describe(HotCorner corner) => corner switch
    {
        HotCorner.TopLeft => "top-left",
        HotCorner.TopRight => "top-right",
        _ => "none",
    };
}

# Agent guide

Tray-only Windows app: rest the pointer in a top corner and the display turns off.
`README.md` is the user-facing document; everything about the internals is here.

## Build, test, deploy

The project targets `net10.0-windows` and builds from any OS with the .NET 10 SDK
(`EnableWindowsTargeting` restores the Windows targeting packs on Linux and macOS).

```bash
dotnet build src/CornerActions/CornerActions.csproj
dotnet publish src/CornerActions/CornerActions.csproj -c Release -o dist   # ~540 KB, needs .NET 10 Desktop Runtime
```

On Windows, `.\build.ps1` does the same and `.\build.ps1 -SelfContained` bundles
the runtime (~47 MB, extracted to `%TEMP%\.net` on first launch).

`scripts\deploy.ps1` installs the result to
`%LOCALAPPDATA%\Programs\CornerActions` and refreshes the desktop shortcut. It
stops the running instance first, because Windows locks a running executable; add
`-Restart` to start it again afterwards.

`scripts\make-icon.ps1` regenerates `assets/app.ico`. The file is committed, so
this only runs when the artwork changes. Frames up to 128 px are DIBs because
System.Drawing on .NET Framework renders PNG-compressed frames as garbage; the
256 px frame stays PNG.

## Layout

| File | Responsibility |
| --- | --- |
| `Program.cs` | Entry point, console attachment, single instance, mode dispatch |
| `AppIcon.cs` | Loads the tray icon from the embedded `assets/app.ico` |
| `StartupOptions.cs` | `--selftest`, `--e2e`, `--dry-run`, `--verbose`, `--help` |
| `TrayAppContext.cs` | Tray icon, 50 ms polling loop, monitor cache, session guards |
| `TrayMenu.cs` | Win32 popup menu: builds the `HMENU` and runs `TrackPopupMenuEx` |
| `CornerDetector.cs` | Pure dwell / hysteresis / cooldown state machine |
| `MonitorLayout.cs` | `EnumDisplayMonitors` enumeration, scope filter, DPI scaling |
| `SessionState.cs` | Locked / full-screen / presentation guard, mouse-button guard |
| `DisplayPower.cs` | `WM_SYSCOMMAND` + `SC_MONITORPOWER` broadcast |
| `AppConfig.cs` | JSON settings, clamping, atomic save |
| `Strings.cs` | English and Simplified Chinese UI text |
| `Log.cs` | English, size-capped file log that never throws |
| `AutoStart.cs` | `HKCU\...\Run` entry |
| `MessageWindow.cs` | Hidden window for `TaskbarCreated` |
| `SelfTest.cs` | Assertions over `CornerDetector`, `Strings`, `AppIcon`, `TrayMenu` |
| `E2ERunner.cs` | Dry-run drive of the real app with a parked pointer |
| `scripts/deploy.ps1` | Installs the published exe and refreshes the desktop shortcut |
| `scripts/make-icon.ps1` | Regenerates `assets/app.ico` |
| `assets/app.ico` | The one copy of the artwork: exe icon and tray icon |

## How the feature works

1. `TrayAppContext` runs a 50 ms `System.Windows.Forms.Timer`. Each tick: return if
   disabled; rebuild the monitor cache if a display change was signalled or the
   cache is empty; refresh the session guard at most every 250 ms; reset the
   detector and return when suppressed or a mouse button is down.
2. `GetCursorPos` supplies the pointer position. A failure (locked desktop, no
   input desktop) skips the tick; it is logged at most once a minute.
3. `CornerDetector.Evaluate` runs the state machine: enter → dwell → fire once →
   latch until the pointer leaves the corner by 4 px, with a 2 s cooldown.
4. `Fire` broadcasts `WM_SYSCOMMAND` with `SC_MONITORPOWER` and `lParam = 2`
   (`--dry-run` logs instead), then writes one log line and raises `Triggered`.
5. `TaskbarCreated` (also sent when the primary display DPI changes) rebuilds the
   cache and re-adds the tray icon; `DisplaySettingsChanged` schedules the same
   rebuild for the next tick.
6. The menu is built on demand as a Win32 `HMENU`; `TrackPopupMenuEx` with
   `TPM_RETURNCMD` returns the chosen command synchronously.

## Invariants

- `CornerDetector` stays pure: no Win32, no clock. Time arrives as an argument.
  Everything the feature promises is verified in `SelfTest.cs` through it.
- All geometry is physical pixels, because the app is `PerMonitorV2`
  (`ApplicationHighDpiMode`). Never mix in logical units. WFO0003 forbids putting
  DPI settings back into `app.manifest`.
- Rebuild the monitor cache on `TaskbarCreated` and on `DisplaySettingsChanged`.
- Session guards run before detection. On a locked desktop `GetCursorPos` can
  fail; treat failure as "skip this tick", not as "no corner".
- `SessionState` suppresses on `QUNS_NOT_PRESENT`, `QUNS_PRESENTATION_MODE` and
  `QUNS_RUNNING_D3D_FULL_SCREEN`, but deliberately not on `QUNS_BUSY`: that value
  also covers ordinary full-screen windows, and a full-screen terminal or video
  player is where a user actually reaches for the corner. Decided after a live
  test with a full-screen Windows Terminal.
- The action is one broadcast of `SC_MONITORPOWER=2` with a 1 s timeout. Log a
  failure and move on; never retry in a loop.
- Logging must never throw into the caller, and must stay in English.
- Log lines are `{utc} {LEVEL} key=value ...`, lowercase keys, no sentences that
  only a human can parse: `trigger corner=top-left action=monitor-off result=ok`,
  `suppressed session=True mouseDown=False`, `layout reason=startup monitors=1`,
  `cursor x=1 y=1 corner=none` (verbose only), `e2e start ... quns=2`.
- UI text lives only in `Strings.cs`; Chinese is chosen by system language with
  English as the fallback.
- Do not add configuration keys for timing internals. Poll interval, cooldown and
  hysteresis are fixed on purpose; a user cannot reason about them and the wrong
  value produces "it fires twice" bugs.

## Settings

`%APPDATA%\CornerActions\config.json`, camelCase, indented, comments and trailing
commas tolerated on read.

| Key | Range / values | Default |
| --- | --- | --- |
| `language` | `auto`, `en`, `zh` | `auto` |
| `enabled` | bool | `true` |
| `monitors` | `primary`, `all` | `primary` |
| `cornerSizeDips` | 1–64 | `4` |
| `dwellMilliseconds` | 0–5000 | `300` |
| `topLeft`, `topRight` | bool | `true` |

`AppConfig.Normalize()` clamps every value on read and on save; unknown or
misspelled values fall back to the defaults. A malformed file is logged, ignored
for that run, and only rewritten when the next setting changes.

## Testing

| Command | Touches the desktop? | Use |
| --- | --- | --- |
| `--selftest` | No | Safe anywhere, including CI |
| `--dry-run` | Shows a tray icon, logs only | Manual checks; never switches the display off |
| `--e2e` | **Moves the real pointer for ~2.5 s** | Ask the human before running it on their machine |

`--e2e` also fails when the session reports `QUNS_NOT_PRESENT`,
`QUNS_PRESENTATION_MODE` or `QUNS_RUNNING_D3D_FULL_SCREEN`, because the app
suppresses actions there. Read the log line `e2e start ... quns=N` before blaming
detection; `quns=2` (an ordinary full-screen window) no longer blocks anything.

Never run a live (non `--dry-run`) trigger while someone is using the machine: it
turns off their screen.

## Platform facts already paid for

- `SC_MONITORPOWER` with `lParam = 2` is the documented "display off" command:
  <https://learn.microsoft.com/windows/win32/menurc/wm-syscommand>
- `TaskbarCreated` is broadcast after a shell restart **and** when the primary
  display DPI changes:
  <https://learn.microsoft.com/windows/win32/shell/taskbar>
- `GetCursorPos` needs the input desktop; it is not valid on the lock screen or a
  secure desktop: <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getcursorpos>
- `SHQueryUserNotificationState` reports full-screen and presentation states, but
  sends no notification when a full-screen app starts or stops, so it has to be
  polled: <https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate>
- `GetDpiForMonitor` is documented as "not DPI aware"; it still reports the real
  per-monitor DPI for a `PerMonitorV2` process, which is why it is used here:
  <https://learn.microsoft.com/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor>
- The tray menu is a Win32 `HMENU`, not a `ContextMenuStrip`: ToolStrip paints its
  own Office-era chrome and ignores the shell's theme. `SetForegroundWindow`
  before and `PostMessage(WM_NULL)` after are not optional - without them the menu
  does not dismiss when the user clicks elsewhere.
- The `Run` value is a command line limited to 260 characters and may be started
  late by the system: <https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys>

## Open questions

- Dark mode for the tray menu would need the undocumented `SetPreferredAppMode`
  (uxtheme ordinal 135) or `SetWindowCompositionAttribute`. The owner runs the
  light theme, so nothing here has been tested in dark mode.
- `monitors: "all"` is covered by unit checks only; it has never run on a real
  multi-monitor desktop.

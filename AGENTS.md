# Agent guide

A tray-only Windows app that turns the display off when the pointer rests in the
top-left or top-right screen corner. `README.md` is the user-facing document; this
file is for whoever changes the code next.

## Build and run

The project targets `net10.0-windows` and builds from any OS with the .NET 10 SDK
(the project sets `EnableWindowsTargeting`, so the Windows targeting packs are
restored on Linux and macOS).

```bash
dotnet build src/CornerActions/CornerActions.csproj
dotnet publish src/CornerActions/CornerActions.csproj -c Release -o dist   # ~540 KB, needs .NET 10 Desktop Runtime
```

On Windows, `.\build.ps1` does the same and `.\build.ps1 -SelfContained` bundles
the runtime (~47 MB). `scripts\deploy.ps1` then installs the result to
`%LOCALAPPDATA%\Programs\CornerActions` and refreshes the desktop shortcut; it
stops the running instance first, because Windows locks a running executable.

## Layout

| File | Responsibility |
| --- | --- |
| `Program.cs` | Entry point, console attachment, single instance, mode dispatch |
| `AppIcon.cs` | Loads the tray icon from the embedded `assets/app.ico` |
| `StartupOptions.cs` | `--selftest`, `--e2e`, `--dry-run`, `--verbose`, `--help` |
| `TrayAppContext.cs` | Tray icon, menu, 50 ms polling loop, monitor cache, session guards |
| `CornerDetector.cs` | Pure dwell / hysteresis / cooldown state machine |
| `MonitorLayout.cs` | `EnumDisplayMonitors` enumeration, scope filter, DPI scaling |
| `SessionState.cs` | Locked / full-screen / presentation guard, mouse-button guard |
| `DisplayPower.cs` | `WM_SYSCOMMAND` + `SC_MONITORPOWER` broadcast |
| `AppConfig.cs` | JSON settings, clamping, atomic save |
| `Strings.cs` | English and Simplified Chinese UI text |
| `Log.cs` | English, size-capped file log that never throws |
| `AutoStart.cs` | `HKCU\...\Run` entry |
| `MessageWindow.cs` | Hidden window for `TaskbarCreated` |
| `SelfTest.cs` | Assertions over `CornerDetector` |
| `E2ERunner.cs` | Dry-run drive of the real app with a parked pointer |
| `scripts/deploy.ps1` | Installs the published exe to `%LOCALAPPDATA%` and refreshes the desktop shortcut |
| `scripts/make-icon.ps1` | Regenerates `assets/app.ico` (only when the artwork changes) |
| `assets/app.ico` | The one copy of the artwork: exe icon and tray icon |

## Invariants

- `CornerDetector` stays pure: no Win32, no clock. Time arrives as an argument.
  Everything the feature promises is verified in `SelfTest.cs` through it.
- All geometry is physical pixels, because the app is `PerMonitorV2`
  (`ApplicationHighDpiMode`). Never mix in logical units. WFO0003 forbids putting
  DPI settings back into `app.manifest`.
- Rebuild the monitor cache on `TaskbarCreated` (the shell also broadcasts it when
  the primary display DPI changes) and on `DisplaySettingsChanged`.
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
- UI text lives only in `Strings.cs`; Chinese is chosen by system language with
  English as the fallback.
- Do not add configuration keys for timing internals. Poll interval, cooldown and
  hysteresis are fixed on purpose; a user cannot reason about them and the wrong
  value produces "it fires twice" bugs.

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
- The `Run` value is a command line limited to 260 characters and may be started
  late by the system: <https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys>

## Open questions

- The live `SC_MONITORPOWER` path has not been exercised on hardware yet; every
  automated run used `--dry-run`.

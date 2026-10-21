# Corner Actions

Hot corners for Windows: park the mouse pointer in the top-left or top-right
corner of the screen and the display turns off, the way macOS hot corners work.

There is no window. The app lives in the notification area with a small menu.

## Requirements

- Windows 10 or Windows 11, x64
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  for the framework-dependent build that ships in `dist/`

The runtime can be installed without downloading a file:

```powershell
winget install Microsoft.DotNet.DesktopRuntime.10
```

## Install and run

There is no prebuilt release yet: build the executable first (see
[Development](#development)); the published file is `dist/CornerActions.exe`.

1. Copy `CornerActions.exe` anywhere you like.
2. Double-click it. A tray icon appears and the feature is on immediately.
3. Optional: right-click the tray icon and enable **Start with Windows**
   (writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, no elevation).

The first run creates:

| Path | Purpose |
| --- | --- |
| `%APPDATA%\CornerActions\config.json` | Settings |
| `%LOCALAPPDATA%\CornerActions\corner-actions.log` | Log, rotated at 512 KB |

## Usage

Move the pointer into the top-left or top-right corner of the primary monitor
and keep it there for 300 ms. The display turns off.

The tray menu is in the Windows display language (Chinese or English, English
otherwise) and contains:

- **Enabled** — pause or resume the feature. Double-clicking the tray icon does
  the same.
- **Top-left corner** / **Top-right corner** — enable one corner, both, or neither.
- **Start with Windows**
- **Open configuration file** / **Reload configuration**
- **Exit**

## Configuration

`%APPDATA%\CornerActions\config.json`, reloaded from the tray menu:

```json
{
  "language": "auto",
  "enabled": true,
  "monitors": "primary",
  "cornerSizeDips": 4,
  "dwellMilliseconds": 300,
  "topLeft": true,
  "topRight": true
}
```

| Key | Values | Meaning |
| --- | --- | --- |
| `language` | `auto`, `en`, `zh` | `auto` follows the Windows display language |
| `enabled` | `true`, `false` | Master switch, same as the tray checkbox |
| `monitors` | `primary`, `all` | Which monitors get hot corners |
| `cornerSizeDips` | 1–64, default 4 | Size of the corner target at 100% scaling; scaled by the monitor DPI |
| `dwellMilliseconds` | 0–5000, default 300 | How long the pointer must stay in the corner |
| `topLeft`, `topRight` | `true`, `false` | Per-corner switches |

Values outside these ranges are clamped on load. A malformed file is logged and
ignored; the app keeps running with defaults, and the file is rewritten the next
time a setting changes.

### Deliberately not configurable

| Behaviour | Value | Why |
| --- | --- | --- |
| Pointer polling | 50 ms | Tight enough for the dwell measurement, no measurable CPU cost |
| Cooldown after firing | 2 s | The next mouse movement wakes the display; a second trigger would fight the user |
| Re-arm distance | 4 px past the corner | Stops a parked pointer from firing repeatedly |
| Locked desktop / full screen / presentation | suppressed | The display must not turn off over a game, a video, or a UAC prompt |

## How it works

- The pointer position is polled with `GetCursorPos` every 50 ms. A global mouse
  hook would see injected input and can stall the whole input queue, so polling is
  used instead.
- Monitor rectangles come from `EnumDisplayMonitors` + `GetMonitorInfo` in
  physical pixels; the corner size is scaled with the monitor DPI. The process is
  per-monitor DPI aware, so these coordinates agree with the cursor position on
  scaled displays.
- A small state machine requires the pointer to stay in the corner for the dwell
  time, then latches the corner until the pointer leaves it. Nothing fires twice
  from one hover.
- The action is the documented system command: `WM_SYSCOMMAND` with
  `SC_MONITORPOWER` and `lParam = 2`, broadcast to top-level windows.
- `TaskbarCreated` (also sent when the primary display DPI changes) rebuilds the
  monitor cache and re-adds the tray icon. `DisplaySettingsChanged` schedules the
  same rebuild for the next poll.

## Known limitations

- Only the top-left and top-right corners, and only one action (display off).
- While a full-screen app, a presentation, or the lock screen is active, the
  corner does nothing. This is intentional.
- A top-positioned taskbar overlaps both corners and makes them hard to reach.
- Turning the display off does not lock the session.
- On multi-monitor setups only the primary monitor is used unless `monitors` is
  set to `all`; adjacent monitors then have corners side by side at the seam.

## Development

Framework dependent build (what ships in `dist/`, ~200 KB):

```powershell
.\build.ps1
```

Portable build with the runtime bundled (~47 MB, extracted to `%TEMP%\.net` on
first launch, slightly slower start):

```powershell
.\build.ps1 -SelfContained
```

Building on Linux or macOS works with the same .NET 10 SDK; the project sets
`EnableWindowsTargeting` so the Windows targeting packs are restored:

```bash
dotnet build src/CornerActions/CornerActions.csproj
```

Checks:

```bash
# Pure logic checks: dwell, hysteresis, cooldown, DPI scaling, multi-monitor. No desktop needed.
CornerActions.exe --selftest

# Real app in dry-run mode. Parks the pointer in the top-left corner for ~2.5 s
# and expects exactly one trigger. It moves your mouse: do not run it while you
# are using the machine.
CornerActions.exe --e2e

# Tray app that logs detections but never turns the display off.
CornerActions.exe --dry-run --verbose
```

Exit code 0 means pass. `--help` lists the switches; run it from a terminal, since
a windowless app has nowhere else to print.

## Troubleshooting

Check `%LOCALAPPDATA%\CornerActions\corner-actions.log`. A trigger looks like:

```
2026-10-21T16:32:09.934Z INFO trigger corner=top-left action=monitor-off result=ok
```

If the log shows `suppressed session=True`, a full-screen app, presentation mode
or the lock screen was active.

## License

MIT

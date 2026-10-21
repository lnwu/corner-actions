# 🖱️ Corner Actions

macOS-style hot corners for Windows. Park the pointer in the top-left or top-right
corner of the screen and the display turns off.

## ✨ What it does

- **Top-left / top-right corner** → display off after a 300 ms dwell
- **No window** — it lives in the notification area
- **Won't fire** while you drag, while the screen is locked, during a
  presentation, or in an exclusive full-screen game
- **One action**, and it is not a lock: the session stays unlocked

## 📦 Install

Windows 10 or 11, x64. Running it needs the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
(`winget install Microsoft.DotNet.DesktopRuntime.10`); building it also needs the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
(`winget install Microsoft.DotNet.SDK.10`).

No release yet, so build it once:

```powershell
.\build.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

That installs to `%LOCALAPPDATA%\Programs\CornerActions` and adds a **Corner
Actions** desktop shortcut. Updating is the same `deploy.ps1` again.

## ✋ Use it

1. Start it from the shortcut. A tray icon appears and the feature is on.
2. Push the pointer into a top corner and keep it there for 0.3 s. The display
   turns off; move the mouse to wake it.
3. Right-click the tray icon for the menu, double-click it to pause or resume.

## ⚙️ Settings

`%APPDATA%\CornerActions\config.json`, reloaded from the tray menu:

| Key | Default | Notes |
| --- | --- | --- |
| `language` | `"auto"` | `auto`, `en`, `zh` |
| `enabled` | `true` | same as the **Enabled** item in the tray menu |
| `monitors` | `"primary"` | `primary` or `all` |
| `cornerSizeDips` | `4` | 1–64, scaled by monitor DPI |
| `dwellMilliseconds` | `300` | 0–5000 |
| `topLeft` / `topRight` | `true` | per-corner switches |

Out-of-range values are clamped. Polling interval, cooldown and re-arm distance
are fixed on purpose.

## 💡 Good to know

- Detection is positional, not window-aware: with the taskbar at the top,
  resting the pointer on the Start button also turns the display off.
- With several monitors, only the primary one has hot corners unless `monitors`
  is `all`.
- Log: `%LOCALAPPDATA%\CornerActions\corner-actions.log`.

## 🗑️ Uninstall

Quit from the tray menu, turn **Start with Windows** off, then delete
`%LOCALAPPDATA%\Programs\CornerActions`, the desktop shortcut,
`%APPDATA%\CornerActions` (settings) and `%LOCALAPPDATA%\CornerActions` (log).

## 📄 License

MIT

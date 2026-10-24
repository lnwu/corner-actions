<#
.SYNOPSIS
    Installs or updates CornerActions from a published build.

.DESCRIPTION
    Copies CornerActions.exe into a stable per-user folder and points a desktop
    shortcut at it. Any running instance is stopped first, because Windows refuses
    to overwrite a running executable.

    The install path never changes, so the desktop shortcut and the "Start with
    Windows" registry entry stay valid across updates. Updating the app is this
    script again; there is no installer and no updater.

    Because the path never changes, the shell's icon cache would keep showing the
    previous artwork on the shortcut. The script tells the shell that the
    executable's icon changed, so a new icon appears without clearing the cache.

.PARAMETER Source
    Published executable to install. Defaults to dist\CornerActions.exe in the
    repository this script belongs to.

.PARAMETER Target
    Install folder. Defaults to %LOCALAPPDATA%\Programs\CornerActions.

.PARAMETER Restart
    Start the app again after copying.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1 -Restart
#>
param(
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) "dist/CornerActions.exe"),
    [string]$Target = (Join-Path $env:LOCALAPPDATA "Programs/CornerActions"),
    [switch]$Restart
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source executable not found: $Source. Run build.ps1 first."
}

$exePath = Join-Path $Target "CornerActions.exe"

$running = @(Get-Process -Name CornerActions -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host ("Stopping {0} running instance(s); the tray icon disappears on hover." -f $running.Count)
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 700
}

New-Item -ItemType Directory -Force -Path $Target | Out-Null
Copy-Item -LiteralPath $Source -Destination $exePath -Force
Write-Host "Installed: $exePath"

$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Corner Actions.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $Target
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = "Corner Actions"
$shortcut.Save()
Write-Host "Shortcut: $shortcutPath"

Add-Type -Namespace CornerActions -Name Shell -MemberDefinition @'
[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
public static extern int Shell_GetCachedImageIndexW(string iconPath, int iconIndex, uint flags);
[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
public static extern void SHUpdateImageW(string hashItem, int index, uint flags, int imageIndex);
[DllImport("shell32.dll")]
public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
'@

# SHUpdateImage marks the cached entry stale; SHCNE_ASSOCCHANGED redraws every view.
$imageIndex = [CornerActions.Shell]::Shell_GetCachedImageIndexW($exePath, 0, 0)
if ($imageIndex -ge 0) {
    [CornerActions.Shell]::SHUpdateImageW($exePath, 0, 0, $imageIndex)
}
[CornerActions.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Host "Icon cache refreshed."

if ($Restart) {
    Start-Process -FilePath $exePath
    Write-Host "Started."
}

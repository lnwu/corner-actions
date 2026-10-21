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

if ($Restart) {
    Start-Process -FilePath $exePath
    Write-Host "Started."
}

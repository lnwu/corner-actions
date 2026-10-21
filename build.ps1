<#
.SYNOPSIS
    Publishes CornerActions.exe into dist/.

.DESCRIPTION
    The default build is framework dependent: about 200 KB and it needs the
    .NET 10 Desktop Runtime on the target machine. Use -SelfContained for a
    portable single file that bundles the runtime (about 47 MB after
    compression, extracted to %TEMP%\.net on first launch).

.EXAMPLE
    .\build.ps1
    .\build.ps1 -SelfContained
#>
param(
    [switch]$SelfContained,
    [string]$Output = "dist"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src/CornerActions/CornerActions.csproj"
$target = Join-Path $PSScriptRoot $Output

if ($SelfContained) {
    dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $target
} else {
    dotnet publish $project -c Release -r win-x64 --self-contained false -o $target
}

Write-Host "published to $target"

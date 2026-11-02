<#
.SYNOPSIS
Publishes the Native AOT WinUI head and packs it with Velopack into artifacts/releases.
.PARAMETER Version
SemVer version of the package (for example 1.0.0 or 0.0.1-test).
#>
param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $publish = Join-Path $root 'artifacts/publish'
    $releases = Join-Path $root 'artifacts/releases'
    Remove-Item $publish, $releases -Recurse -Force -ErrorAction SilentlyContinue

    dotnet publish src/Squill.App.WinUI -c Release -r win-x64 -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

    dotnet vpk pack --packId SQuilL --packVersion $Version --packDir $publish `
        --mainExe Squill.App.WinUI.exe --icon assets/brand/app-icons/windows/squill.ico --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }
}
finally {
    Pop-Location
}

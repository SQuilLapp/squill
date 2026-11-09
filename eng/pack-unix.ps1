<#
.SYNOPSIS
Publishes the Native AOT Avalonia head for Linux or macOS and packs it with Velopack. Runs on the target OS only (ubuntu / macos runner).
.DESCRIPTION
linux-x64: `vpk pack` builds an AppImage and the update feed, for the channel stable-linux-x64 (and beta-linux-x64 with -AlsoBeta).
osx-arm64: `vpk bundle` builds the .app and the script zips it (SQuilL-<version>-osx-arm64.zip); there is no feed and no automatic update
on macOS yet. The OS is part of the channel name off Windows because Velopack file names carry no OS and would collide with the
Windows packages in the one GitHub Release. Nothing is signed or notarized. No delta packages (no previous-release download).
.PARAMETER Runtime
linux-x64 or osx-arm64. Must match the OS the script runs on.
.PARAMETER Version
SemVer 2 version. Default: eng/get-version.ps1.
.PARAMETER Channel
stable or beta. Default: beta when the version has a pre-release suffix, otherwise stable.
.PARAMETER AlsoBeta
Linux, stable versions only: also packs the beta channel.
.PARAMETER SelfTest
Checks the channel and file naming and throws on any mismatch.
#>
param(
    [ValidateSet('linux-x64', 'osx-arm64')][string]$Runtime,
    [string]$Version,
    [ValidateSet('stable', 'beta')][string]$Channel,
    [string]$PackId = 'SQuilL',
    [switch]$AlsoBeta,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

# stable-linux-x64, beta-osx-arm64: the OS infix mirrors VelopackUpdateEngine.ChannelName.
function Get-ChannelName([string]$Channel, [string]$Runtime) {
    $os, $arch = $Runtime -split '-'
    "$Channel-$os-$arch"
}

if ($SelfTest) {
    if ((Get-ChannelName 'stable' 'linux-x64') -ne 'stable-linux-x64') { throw 'Self-test: linux channel name.' }
    if ((Get-ChannelName 'beta' 'osx-arm64') -ne 'beta-osx-arm64') { throw 'Self-test: osx channel name.' }
    'Self-test passed.'
    exit 0
}

if (-not $Runtime) { throw '-Runtime is required (linux-x64 or osx-arm64).' }
if (($Runtime -eq 'linux-x64' -and -not $IsLinux) -or ($Runtime -eq 'osx-arm64' -and -not $IsMacOS)) {
    throw "$Runtime can only be packed on its own OS (the release workflow uses ubuntu-latest and macos-latest)."
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $propsVersion = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'get-version.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'eng/get-version.ps1 failed.' }
    if (-not $Version) { $Version = $propsVersion }
    if ($Version -cne $propsVersion) { throw "Version '$Version' differs from eng/Version.props ('$propsVersion')." }
    $preRelease = $Version.Contains('-')
    if (-not $Channel) { $Channel = $preRelease ? 'beta' : 'stable' }
    if ($Channel -eq 'stable' -and $preRelease) { throw "Pre-release '$Version' cannot be packed for the stable channel." }
    $channels = @(Get-ChannelName $Channel $Runtime)
    if ($AlsoBeta -and $Channel -eq 'stable' -and $Runtime -eq 'linux-x64') { $channels += Get-ChannelName 'beta' $Runtime }

    $publish = Join-Path $root "artifacts/publish-$Runtime"
    $releases = Join-Path $root "artifacts/releases-$Runtime"
    $notes = Join-Path $root 'artifacts/release-notes.md'
    New-Item -ItemType Directory (Split-Path $notes) -Force | Out-Null
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'release-notes.ps1') -Version $Version -OutFile $notes
    if ($LASTEXITCODE -ne 0) { throw "No usable release notes for $Version in CHANGELOG.md." }

    Remove-Item $publish, $releases -Recurse -Force -ErrorAction SilentlyContinue
    $log = & dotnet publish src/Squill.App.Avalonia -c Release -r $Runtime -o $publish "-p:Version=$Version" 2>&1
    $exit = $LASTEXITCODE
    $log | ForEach-Object { "$_" }
    if ($exit -ne 0) { throw 'dotnet publish failed.' }
    # Same rule as pack-windows.ps1: only the accepted assembly-level IL2104/IL3053 of NuGet packages.
    $accepted = '\.nuget[\\/]packages[\\/].*: warning IL(2104|3053):'
    $trim = @($log | Where-Object { "$_" -match '\bIL[23]\d{3}\b' -and "$_" -notmatch $accepted })
    if ($trim.Count -gt 0) { throw "dotnet publish reported trim/AOT warnings:`n$($trim -join "`n")" }
    if (-not (Test-Path (Join-Path $publish 'Squill.App.Avalonia'))) { throw 'Squill.App.Avalonia was not published.' }
    New-Item -ItemType Directory $releases -Force | Out-Null

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    foreach ($c in $channels) {
        if ($Runtime -eq 'linux-x64') {
            dotnet vpk pack --packId $PackId --packVersion $Version --packTitle SQuilL --packAuthors SQuilL `
                --packDir $publish --mainExe Squill.App.Avalonia --icon assets/brand/app-icons/linux/hicolor/256x256/apps/squill.png `
                --runtime $Runtime --channel $c --releaseNotes $notes --outputDir $releases
            if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for channel $c." }
            foreach ($name in "releases.$c.json", "$PackId-$Version-$c-full.nupkg") {
                if (-not (Test-Path (Join-Path $releases $name))) { throw "Pack output is missing '$name' in $releases." }
            }
        }
        else {
            # The debug symbols folder is a nested bundle: it breaks codesign and adds 140 MB.
            Get-ChildItem $publish -Filter '*.dSYM' -Directory | Remove-Item -Recurse -Force
            dotnet vpk bundle --packId $PackId --packVersion $Version --packTitle SQuilL --packAuthors SQuilL `
                --packDir $publish --mainExe Squill.App.Avalonia --icon assets/brand/app-icons/macos/squill.icns `
                --runtime $Runtime --channel $c --outputDir $releases
            if ($LASTEXITCODE -ne 0) { throw "vpk bundle failed for channel $c." }
            $app = Get-ChildItem $releases -Directory -Filter '*.app' | Select-Object -First 1
            if (-not $app) { throw "vpk bundle produced no .app in $releases." }
            # vpk writes Info.plist with a UTF-8 BOM, which macOS can reject ("is damaged"): rewrite it without.
            $plist = Join-Path $app.FullName 'Contents/Info.plist'
            [IO.File]::WriteAllText($plist, [IO.File]::ReadAllText($plist), [Text.UTF8Encoding]::new($false))
            # Ad hoc signature (no certificate): Apple Silicon refuses unsigned code. Not a notarization, Gatekeeper still warns.
            & codesign --force --deep -s - $app.FullName
            if ($LASTEXITCODE -ne 0) { throw 'codesign (ad hoc) failed.' }
            & codesign --verify --deep --strict $app.FullName
            if ($LASTEXITCODE -ne 0) { throw 'codesign verification failed.' }
            $zip = Join-Path $releases "$PackId-$Version-$Runtime.zip"
            & ditto -c -k --keepParent $app.FullName $zip
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path $zip)) { throw 'zip of the .app failed.' }
            Remove-Item $app.FullName -Recurse -Force
        }
    }
    Get-ChildItem $releases -File | Select-Object Name, @{ n = 'KB'; e = { [math]::Round($_.Length / 1KB) } } | Format-Table -AutoSize
    "Packed $PackId $Version for $($channels -join ', ')."
}
finally {
    Pop-Location
}

<#
.SYNOPSIS
Publishes the Native AOT WinUI head and packs it with Velopack: the one release build entry point (developer and CI).
.DESCRIPTION
Steps: validate the version, release notes from CHANGELOG.md, AOT publish (fails on any IL2xxx/IL3xxx line except the
accepted assembly-level IL2104/IL3053 of NuGet packages), dotnet tool restore,
optional download of the previous release per channel (so vpk can build deltas), vpk pack per channel, output check.
Velopack channels carry the architecture (stable-x64, beta-x64). A stable version is packed for stable-x64 and, with -AlsoBeta,
also for beta-x64; a pre-release (1.3.0-beta.1) is packed for beta-x64 only and ignores -AlsoBeta.
The notes of the version must exist in CHANGELOG.md (eng/release-notes.ps1), so a local pack of the current eng/Version.props
version needs its section. Signing is off unless -Sign is given (see below).
.PARAMETER Version
SemVer 2 version. Default: eng/get-version.ps1. A different value needs -AllowVersionOverride.
.PARAMETER Channel
stable or beta. Default: beta when the version has a pre-release suffix, otherwise stable.
.PARAMETER Runtime
Runtime identifier; only win-x64 for now.
.PARAMETER PackId
Velopack pack id. Never change it: it breaks updates.
.PARAMETER OutputDir
vpk output directory, relative to the repository root unless rooted.
.PARAMETER FromGithub
Downloads the previous release of each packed channel before packing. Uses GITHUB_TOKEN or `gh auth token` when available, else anonymous.
.PARAMETER SkipPublish
Reuses artifacts/publish and keeps the content of OutputDir.
.PARAMETER AlsoBeta
Stable versions only: packs the same publish output for beta-x64 as well.
.PARAMETER AllowVersionOverride
Allows a -Version different from eng/Version.props, and falls back to the Unreleased notes when the version has no usable
CHANGELOG section (local checks only).
.PARAMETER Sign
Signs the package (off by default; without it the output is unsigned exactly as before). The mode comes from environment
variables only, never from files or defaults; the script fails before the publish when none is configured and names the
missing variable, never a value:
  1. Azure Trusted Signing: AZURE_TRUSTED_SIGNING_METADATA_FILE = path of the metadata.json (endpoint, account, profile);
     the credentials come from the environment of the Azure login (OIDC federated identity preferred over a client secret).
  2. PFX certificate: SQUILL_SIGN_PFX_BASE64 (base64 of the .pfx) and SQUILL_SIGN_PFX_PASSWORD; the script decodes the file
     to a temporary path and deletes it afterwards. Timestamp server: SQUILL_SIGN_TIMESTAMP_URL (default http://timestamp.digicert.com).
After the pack, Squill.App.WinUI.exe inside each full package and each Setup exe must have an Authenticode status of Valid.
SQUILL_SIGN_ALLOW_UNTRUSTED=1 (test aid for a throwaway self-signed certificate, never in releases) also accepts NotTrusted and
UnknownError (what Windows reports for a self-signed root); NotSigned and HashMismatch always fail.
The password and the metadata path are never printed: vpk output is scrubbed.
.PARAMETER SelfTest
Runs the output check over temporary directories and throws on any mismatch.
#>
param(
    [string]$Version,
    [ValidateSet('stable', 'beta')][string]$Channel,
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$PackId = 'SQuilL',
    [string]$OutputDir = 'artifacts/releases',
    [switch]$FromGithub,
    [switch]$SkipPublish,
    [switch]$AlsoBeta,
    [switch]$AllowVersionOverride,
    [switch]$Sign,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repoUrl = 'https://github.com/squillapp/squill'
$semVer = '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$'

function Assert-PackOutput([string]$Dir, [string]$PackId, [string]$Version, [string]$VpkChannel, [bool]$ExpectDelta) {
    $setup = "$PackId-$VpkChannel-Setup.exe"
    $full = "$PackId-$Version-$VpkChannel-full.nupkg"
    $feed = "releases.$VpkChannel.json"
    foreach ($name in $setup, $full, $feed) {
        if (-not (Test-Path (Join-Path $Dir $name))) { throw "Pack output is missing '$name' in $Dir." }
    }
    if ((Get-Content -Raw (Join-Path $Dir $feed)) -notmatch [regex]::Escape($Version)) { throw "$feed does not list version $Version." }
    $delta = "$PackId-$Version-$VpkChannel-delta.nupkg"
    if ($ExpectDelta -and -not (Test-Path (Join-Path $Dir $delta))) { throw "Pack output is missing the delta '$delta' in $Dir." }
}

# Reads the signing mode from the variables handed by $GetVar; throws naming the missing variable, never a value.
function Get-SigningConfig([scriptblock]$GetVar) {
    $metadata = & $GetVar 'AZURE_TRUSTED_SIGNING_METADATA_FILE'
    if ($metadata) {
        if (-not (Test-Path $metadata)) { throw 'AZURE_TRUSTED_SIGNING_METADATA_FILE does not point to an existing file.' }
        return [pscustomobject]@{ Mode = 'azure'; Metadata = $metadata; PfxBase64 = $null; Password = $null; Timestamp = $null }
    }
    $pfx = & $GetVar 'SQUILL_SIGN_PFX_BASE64'
    if (-not $pfx) { throw '-Sign needs a signing configuration: set AZURE_TRUSTED_SIGNING_METADATA_FILE, or SQUILL_SIGN_PFX_BASE64 and SQUILL_SIGN_PFX_PASSWORD.' }
    $password = & $GetVar 'SQUILL_SIGN_PFX_PASSWORD'
    if (-not $password) { throw 'SQUILL_SIGN_PFX_PASSWORD is not set.' }
    $timestamp = & $GetVar 'SQUILL_SIGN_TIMESTAMP_URL'
    if (-not $timestamp) { $timestamp = 'http://timestamp.digicert.com' }
    [pscustomobject]@{ Mode = 'pfx'; Metadata = $null; PfxBase64 = $pfx; Password = $password; Timestamp = $timestamp }
}

# Checks the Authenticode status of the Setup exe and of the app exe inside the full package.
function Assert-Signed([string]$Dir, [string]$PackId, [string]$Version, [string]$VpkChannel, [bool]$AllowUntrusted) {
    $extract = Join-Path ([IO.Path]::GetTempPath()) "squill-verify-$([guid]::NewGuid().ToString('N'))"
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $Dir "$PackId-$Version-$VpkChannel-full.nupkg"), $extract)
        $targets = @((Join-Path $Dir "$PackId-$VpkChannel-Setup.exe"))
        $targets += @(Get-ChildItem $extract -Recurse -Filter 'Squill.App.WinUI.exe' | ForEach-Object FullName)
        if ($targets.Count -lt 2) { throw "Squill.App.WinUI.exe was not found inside the $VpkChannel package." }
        foreach ($file in $targets) {
            $status = [string](Get-AuthenticodeSignature $file).Status
            if (-not (Test-SignatureAccepted $status $AllowUntrusted)) { throw "'$(Split-Path -Leaf $file)' has signature status $status (expected Valid)." }
        }
    }
    finally { Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue }
}

function Test-SignatureAccepted([string]$Status, [bool]$AllowUntrusted) {
    $Status -ceq 'Valid' -or ($AllowUntrusted -and $Status -cin 'NotTrusted', 'UnknownError')
}

if ($SelfTest) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "pack-selftest-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory $tmp | Out-Null
    try {
        $files = 'P-beta-x64-Setup.exe', 'P-1.0.0-beta-x64-full.nupkg', 'releases.beta-x64.json', 'P-1.0.0-beta-x64-delta.nupkg'
        foreach ($f in $files) { Set-Content (Join-Path $tmp $f) '{"Version":"1.0.0"}' }
        Assert-PackOutput $tmp 'P' '1.0.0' 'beta-x64' $true
        foreach ($f in $files) {
            $path = Join-Path $tmp $f
            Remove-Item $path
            $failed = $false
            try { Assert-PackOutput $tmp 'P' '1.0.0' 'beta-x64' $true } catch { $failed = $_.Exception.Message -like "*$f*" }
            if (-not $failed) { throw "Self-test: missing '$f' was not reported." }
            Set-Content $path '{"Version":"1.0.0"}'
        }
        Set-Content (Join-Path $tmp 'releases.beta-x64.json') '{"Version":"2.0.0"}'
        $failed = $false
        try { Assert-PackOutput $tmp 'P' '1.0.0' 'beta-x64' $false } catch { $failed = $true }
        if (-not $failed) { throw 'Self-test: a feed without the version was accepted.' }
    }
    finally { Remove-Item $tmp -Recurse -Force }
    $vars = @{}
    $get = { param($n) $vars[$n] }
    $failed = ''
    try { Get-SigningConfig $get | Out-Null } catch { $failed = $_.Exception.Message }
    if ($failed -notlike '*AZURE_TRUSTED_SIGNING_METADATA_FILE*SQUILL_SIGN_PFX_BASE64*') { throw 'Self-test: an empty signing configuration was not reported.' }
    $vars['SQUILL_SIGN_PFX_BASE64'] = 'AAAA'
    $failed = ''
    try { Get-SigningConfig $get | Out-Null } catch { $failed = $_.Exception.Message }
    if ($failed -ne 'SQUILL_SIGN_PFX_PASSWORD is not set.') { throw 'Self-test: a missing password was not reported.' }
    $vars['SQUILL_SIGN_PFX_PASSWORD'] = 'secret-value'
    $config = Get-SigningConfig $get
    if ($config.Mode -ne 'pfx' -or $config.Timestamp -ne 'http://timestamp.digicert.com') { throw 'Self-test: the PFX mode or its default timestamp is wrong.' }
    if (-not (Test-SignatureAccepted 'Valid' $false) -or (Test-SignatureAccepted 'NotTrusted' $false) -or -not (Test-SignatureAccepted 'NotTrusted' $true) -or (Test-SignatureAccepted 'UnknownError' $false) -or -not (Test-SignatureAccepted 'UnknownError' $true) -or (Test-SignatureAccepted 'NotSigned' $true)) { throw 'Self-test: the signature status rule is wrong.' }
    'Self-test passed.'
    exit 0
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $propsVersion = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'get-version.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'eng/get-version.ps1 failed.' }
    if (-not $Version) { $Version = $propsVersion }
    if ($Version -notmatch $semVer) { throw "Version '$Version' is not a valid SemVer 2 version." }
    if ($Version -cne $propsVersion -and -not $AllowVersionOverride) {
        throw "Version '$Version' differs from eng/Version.props ('$propsVersion'); pass -AllowVersionOverride for a local check."
    }
    $preRelease = $Version.Contains('-')
    if (-not $Channel) { $Channel = $preRelease ? 'beta' : 'stable' }
    if ($Channel -eq 'stable' -and $preRelease) { throw "Pre-release '$Version' cannot be packed for the stable channel." }
    $arch = $Runtime -replace '^win-', ''
    $channels = @("$Channel-$arch")
    if ($AlsoBeta -and $Channel -eq 'stable') { $channels += "beta-$arch" }

    $signing = $Sign ? (Get-SigningConfig { param($n) [Environment]::GetEnvironmentVariable($n) }) : $null
    $publish = Join-Path $root 'artifacts/publish'
    $releases = [IO.Path]::GetFullPath($OutputDir, $root)
    $notes = Join-Path $root 'artifacts/release-notes.md'
    New-Item -ItemType Directory (Split-Path $notes) -Force | Out-Null

    # Notes first: a missing CHANGELOG section must fail before the long publish.
    $notesVersion = $Version
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'release-notes.ps1') -Version $Version -OutFile $notes
    if ($LASTEXITCODE -ne 0) {
        if (-not $AllowVersionOverride) { throw "No usable release notes for $Version in CHANGELOG.md." }
        $notesVersion = 'Unreleased'
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'release-notes.ps1') -Version Unreleased -OutFile $notes
        if ($LASTEXITCODE -ne 0) {
            # The Unreleased section holds its placeholder: a local check still needs some notes.
            Set-Content $notes "### Changed`n`n- Local test build." -Encoding utf8NoBOM
        }
    }

    if (-not $SkipPublish) {
        Remove-Item $publish, $releases -Recurse -Force -ErrorAction SilentlyContinue
        $log = & dotnet publish src/Squill.App.WinUI -c Release -r $Runtime -o $publish "-p:Version=$Version" 2>&1
        $publishExit = $LASTEXITCODE
        $log | ForEach-Object { "$_" }
        if ($publishExit -ne 0) { throw 'dotnet publish failed.' }
        # Known and accepted: assembly-level IL2104/IL3053 of NuGet packages (docs/poc/aot-drivers.md); anything else fails.
        $accepted = '\.nuget[\\/]packages[\\/].*: warning IL(2104|3053):'
        $trim = @($log | Where-Object { "$_" -match '\bIL[23]\d{3}\b' -and "$_" -notmatch $accepted })
        if ($trim.Count -gt 0) { throw "dotnet publish reported trim/AOT warnings:`n$($trim -join "`n")" }
    }
    elseif (-not (Test-Path $publish)) { throw 'artifacts/publish does not exist; run without -SkipPublish first.' }
    New-Item -ItemType Directory $releases -Force | Out-Null

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    $previous = @{}
    if ($FromGithub) {
        $token = $env:GITHUB_TOKEN
        if (-not $token -and (Get-Command gh -ErrorAction SilentlyContinue)) { $token = (& gh auth token 2>$null) }
        foreach ($c in $channels) {
            $before = @(Get-ChildItem $releases -Filter "*-$c-full.nupkg").Count
            $download = @('vpk', 'download', 'github', '--repoUrl', $repoUrl, '--channel', $c, '--outputDir', $releases)
            if ($c -like 'beta-*') { $download += '--pre' }
            if ($token) { $download += @('--token', $token) }
            & dotnet @download
            if ($LASTEXITCODE -ne 0) { throw "vpk download failed for channel $c." }
            # vpk download exits 0 when nothing was found: only the files tell.
            $previous[$c] = @(Get-ChildItem $releases -Filter "*-$c-full.nupkg").Count -gt $before
        }
    }

    $signArgs = @()
    $scrub = @()
    if ($signing -and $signing.Mode -eq 'azure') { $signArgs = @('--azureTrustedSignFile', $signing.Metadata); $scrub = @($signing.Metadata) }
    if ($signing -and $signing.Mode -eq 'pfx') {
        $pfxFile = Join-Path ([IO.Path]::GetTempPath()) "squill-sign-$([guid]::NewGuid().ToString('N')).pfx"
        [IO.File]::WriteAllBytes($pfxFile, [Convert]::FromBase64String($signing.PfxBase64))
        $signParams = "/f `"$pfxFile`" /p `"$($signing.Password)`" /fd sha256 /tr $($signing.Timestamp) /td sha256"
        $signArgs = @('--signParams', $signParams)
        $scrub = @($signing.Password, $pfxFile)
    }
    $allowUntrusted = $env:SQUILL_SIGN_ALLOW_UNTRUSTED -eq '1'

    try {
        foreach ($c in $channels) {
            # The command line is never printed; with signing on, vpk output has the secrets replaced.
            $packLog = dotnet vpk pack --packId $PackId --packVersion $Version --packTitle SQuilL --packAuthors SQuilL `
                --packDir $publish --mainExe Squill.App.WinUI.exe --icon assets/brand/app-icons/windows/squill.ico `
                --runtime $Runtime --channel $c --releaseNotes $notes --outputDir $releases @signArgs 2>&1
            $packExit = $LASTEXITCODE
            foreach ($line in $packLog) {
                $text = "$line"
                foreach ($secret in $scrub) { $text = $text.Replace($secret, '***') }
                $text
            }
            if ($packExit -ne 0) { throw "vpk pack failed for channel $c." }
            Assert-PackOutput $releases $PackId $Version $c ([bool]$previous[$c])
            if ($signing) { Assert-Signed $releases $PackId $Version $c $allowUntrusted }
        }
    }
    finally {
        if ($pfxFile) { Remove-Item $pfxFile -Force -ErrorAction SilentlyContinue }
    }

    Get-ChildItem $releases -File | Select-Object Name, @{ n = 'KB'; e = { [math]::Round($_.Length / 1KB) } } | Format-Table -AutoSize
    "Packed $PackId $Version for $($channels -join ', ') (notes: $notesVersion)."
}
finally {
    Pop-Location
}

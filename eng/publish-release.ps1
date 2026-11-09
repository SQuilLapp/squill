<#
.SYNOPSIS
Creates the GitHub Release of a version tag and uploads the pack output, update feed last.
.DESCRIPTION
Order matters for update safety: a client reads releases.<channel>.json first, so that file must never list a package that
is not yet downloadable. The Release is created as a draft, the packages and installers are uploaded (sequential calls),
then the feed files (releases.*.json, assets.*.json, RELEASES*), and only then the draft is published. A failed run leaves a
draft that the maintainer deletes by hand (docs/release.md). An existing Release for the tag stops the script: nothing is
overwritten (no --clobber). Needs GH_TOKEN (the automatic workflow token) unless -DryRun.
.PARAMETER Tag
The release tag, for example v0.1.0.
.PARAMETER Prerelease
Marks the GitHub Release as a pre-release (version with a -beta.N suffix).
.PARAMETER ReleasesDir
The vpk output directory.
.PARAMETER NotesFile
Markdown file with the release notes.
.PARAMETER Target
Optional commit the tag is created on when it does not exist yet (the workflow passes GITHUB_SHA).
.PARAMETER DryRun
Prints the gh commands in order and runs none.
.PARAMETER SelfTest
Checks the upload order over a temporary directory and throws on any mismatch.
#>
param(
    [string]$Tag,
    [switch]$Prerelease,
    [string]$ReleasesDir = 'artifacts/releases',
    [string]$NotesFile = 'artifacts/release-notes.md',
    [string]$Target,
    [switch]$DryRun,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$feedPattern = '^(releases\..+\.json|assets\..+\.json|RELEASES.*)$'

function Get-UploadGroups([string]$Dir) {
    $files = @(Get-ChildItem $Dir -File)
    $feed = @($files | Where-Object { $_.Name -match $feedPattern } | Sort-Object Name)
    $packages = @($files | Where-Object { $_.Name -notmatch $feedPattern } | Sort-Object Name)
    if ($feed.Count -eq 0) { throw "No update feed file (releases.<channel>.json) in $Dir." }
    if ($packages.Count -eq 0) { throw "No package or installer in $Dir." }
    [pscustomobject]@{ Packages = $packages.FullName; Feed = $feed.FullName }
}

function Get-ReleaseCommands([string]$Tag, [bool]$Pre, [string]$NotesFile, [string]$Target, $Groups) {
    $create = @('release', 'create', $Tag, '--draft', '--title', "SQuilL $($Tag.TrimStart('v'))", '--notes-file', $NotesFile)
    if ($Pre) { $create += '--prerelease' }
    if ($Target) { $create += @('--target', $Target) }
    , $create
    , (@('release', 'upload', $Tag) + $Groups.Packages)
    , (@('release', 'upload', $Tag) + $Groups.Feed)
    , @('release', 'edit', $Tag, '--draft=false')
}

if ($SelfTest) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "publish-selftest-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory $tmp | Out-Null
    try {
        foreach ($f in 'releases.stable-x64.json', 'P-1.0.0-stable-x64-full.nupkg', 'P-stable-x64-Setup.exe', 'releases.beta-x64.json') { Set-Content (Join-Path $tmp $f) 'x' }
        $groups = Get-UploadGroups $tmp
        $commands = @(Get-ReleaseCommands 'v1.0.0' $false 'n.md' '' $groups)
        if ($commands.Count -ne 4 -or $commands[0] -notcontains '--draft') { throw 'Self-test: the Release must be created as a draft.' }
        if (@($commands[1] | Where-Object { $_ -like '*.json' }).Count -ne 0) { throw 'Self-test: a feed file is uploaded with the packages.' }
        if (@($commands[2] | Where-Object { $_ -like '*.json' }).Count -ne 2 -or @($commands[2] | Where-Object { $_ -like '*.nupkg' }).Count -ne 0) { throw 'Self-test: the feed upload must hold only feed files.' }
        if ($commands[3] -notcontains '--draft=false') { throw 'Self-test: the Release must be published last.' }
        if ((Get-ReleaseCommands 'v1.0.0-beta.1' $true 'n.md' 'abc' $groups)[0] -notcontains '--prerelease') { throw 'Self-test: a pre-release flag is missing.' }
        Remove-Item (Join-Path $tmp 'releases.*.json')
        $failed = $false
        try { Get-UploadGroups $tmp | Out-Null } catch { $failed = $true }
        if (-not $failed) { throw 'Self-test: a missing feed was not reported.' }
        'Self-test passed.'
    }
    finally { Remove-Item $tmp -Recurse -Force }
    return
}

if ($Tag -notmatch '^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Tag '$Tag' is not v<SemVer 2>." }
if ($Prerelease -ne ($Tag -match '-')) { throw "-Prerelease does not match tag '$Tag'." }
if (-not (Test-Path $NotesFile)) { throw "Release notes file '$NotesFile' does not exist." }
$commands = Get-ReleaseCommands $Tag ([bool]$Prerelease) $NotesFile $Target (Get-UploadGroups $ReleasesDir)

if ($DryRun) {
    foreach ($c in $commands) { 'gh ' + ($c -join ' ') }
    return
}

gh release view $Tag *> $null
if ($LASTEXITCODE -eq 0) { throw "A Release for $Tag already exists; refusing to overwrite it." }
foreach ($c in $commands) {
    & gh @c
    if ($LASTEXITCODE -ne 0) { throw "gh $($c[0..1] -join ' ') failed; the Release stays a draft." }
}
"Published $Tag."

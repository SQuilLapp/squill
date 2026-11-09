<#
.SYNOPSIS
Prints the release notes of one version from CHANGELOG.md, without the section heading.
.DESCRIPTION
CHANGELOG.md follows "Keep a Changelog": "## [Unreleased]" first, then "## [<SemVer>] - <yyyy-MM-dd>" sections with
"### Added|Changed|Fixed|Removed" subsections. Only the markdown the app can render is allowed: "###" headings, "-" bullets,
paragraphs, **bold**, `code` and [text](https://...) links (no tables, images, HTML or nested lists).
Exits 1 when the section is missing or empty, still holds the "_Nothing yet._" placeholder, or uses unsupported markdown.
A pre-release has its own section; there is no fallback to another version.
.PARAMETER Version
A SemVer 2 version (for example 0.1.0 or 0.2.0-beta.1) or "Unreleased".
.PARAMETER OutFile
Writes the notes there (UTF-8 without BOM, LF newlines) instead of printing them.
.PARAMETER SelfTest
Runs the parser over an in-script sample and throws on any mismatch.
#>
param([string]$Version, [string]$OutFile, [switch]$SelfTest)

$ErrorActionPreference = 'Stop'

function Get-ReleaseNotes([string[]]$Lines, [string]$Version) {
    $start = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^## \[(.+?)\]' -and $Matches[1] -ceq $Version) { $start = $i + 1; break }
    }
    if ($start -lt 0) { throw "CHANGELOG.md has no section '## [$Version]'." }
    $body = [System.Collections.Generic.List[string]]::new()
    for ($i = $start; $i -lt $Lines.Count -and $Lines[$i] -notmatch '^## '; $i++) { $body.Add($Lines[$i].TrimEnd()) }
    while ($body.Count -gt 0 -and $body[0] -eq '') { $body.RemoveAt(0) }
    while ($body.Count -gt 0 -and $body[$body.Count - 1] -eq '') { $body.RemoveAt($body.Count - 1) }
    if ($body.Count -eq 0) { throw "Section '## [$Version]' is empty." }
    foreach ($line in $body) {
        $plain = $line -replace '`[^`]*`', ''
        if ($line -match '^\s*_Nothing yet\._\s*$') { throw "Section '## [$Version]' still holds the placeholder '_Nothing yet._'." }
        if ($line -match '^\s*\|') { throw "Unsupported markdown (table) in '## [$Version]': $line" }
        if ($plain -match '</?[A-Za-z][^>]*>') { throw "Unsupported markdown (HTML) in '## [$Version]': $line" }
        if ($plain -match '!\[') { throw "Unsupported markdown (image) in '## [$Version]': $line" }
        if ($line -match '^\s+[-*] ') { throw "Unsupported markdown (nested list) in '## [$Version]': $line" }
    }
    $body -join "`n"
}

if ($SelfTest) {
    $sample = @(
        '## [Unreleased]', '', '_Nothing yet._', '',
        '## [1.0.0] - 2026-01-01', '', '### Added', '- A `List<T>` and **bold** [link](https://example.com)', '',
        '## [1.1.0] - 2026-02-01', '',
        '## [1.2.0] - 2026-03-01', '| a | b |', '',
        '## [1.3.0] - 2026-04-01', '<b>x</b>', '',
        '## [1.4.0] - 2026-05-01', '![img](x.png)', ''
    )
    $good = Get-ReleaseNotes $sample '1.0.0'
    if ($good -cne "### Added`n- A ``List<T>`` and **bold** [link](https://example.com)") { throw "Self-test: good section returned '$good'." }
    foreach ($bad in 'Unreleased', '9.9.9', '1.1.0', '1.2.0', '1.3.0', '1.4.0') {
        $failed = $false
        try { $null = Get-ReleaseNotes $sample $bad } catch { $failed = $true }
        if (-not $failed) { throw "Self-test: version '$bad' should have been rejected." }
    }
    'Self-test passed.'
    exit 0
}

if (-not $Version) { [Console]::Error.WriteLine('Pass -Version <semver|Unreleased> or -SelfTest.'); exit 1 }
try {
    $notes = Get-ReleaseNotes (Get-Content (Join-Path $PSScriptRoot '..' 'CHANGELOG.md') -Encoding utf8) $Version
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
if ($OutFile) { [System.IO.File]::WriteAllText($OutFile, $notes + "`n", [System.Text.UTF8Encoding]::new($false)) } else { $notes }

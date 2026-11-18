#requires -Version 7
<#
.SYNOPSIS
  Publishes a head with Native AOT and fails on any trim/AOT warning (ILxxxx) that is not in eng/aot-warnings.baseline.txt.
.DESCRIPTION
  Exit codes: 0 only baseline warnings, 1 a warning outside the baseline (or from src/ or tools/), 2 the publish failed.
  -LogFile parses an existing publish log instead of publishing (used by the self-test and for saved CI logs).
.EXAMPLE
  pwsh eng/check-aot-warnings.ps1 -Head All
  pwsh eng/check-aot-warnings.ps1 -LogFile eng/testdata/aot-clean.log
#>
[CmdletBinding()]
param(
    [ValidateSet('WinUI', 'Avalonia', 'All')][string]$Head = 'All',
    [string]$LogFile
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$baselineFile = Join-Path $PSScriptRoot 'aot-warnings.baseline.txt'
$baseline = @(Get-Content $baselineFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })

function Get-Warnings([string[]]$Lines) {
    foreach ($line in $Lines) {
        # "<file>(l,c): warning ILxxxx: ..." or "<file> : warning ILxxxx: ..." (ILC: "... AOT analysis warning ILxxxx")
        if ($line -notmatch '^(?<file>.*?)(?:\(\d+(?:,\d+)*\))?\s*:\s*(?:[\w ]+ )?warning (?<code>IL\d{4})') { continue }
        $file = $Matches['file'].Trim()
        # shortcut: a Squill source file is recognised by its path; ILC warnings without a path are keyed "ILC" and need a baseline entry.
        [pscustomobject]@{
            Key    = "$($Matches['code']) $(Split-Path -Leaf $file)"
            IsOurs = $file -match '[\\/](src|tools)[\\/]'
            Line   = $line.Trim()
        }
    }
}

$found = @()
if ($LogFile) {
    $found += Get-Warnings (Get-Content $LogFile)
}
else {
    $heads = if ($Head -eq 'All') { 'WinUI', 'Avalonia' } else { , $Head }
    $out = Join-Path $root 'artifacts/aot-check'
    try {
        foreach ($h in $heads) {
            $dir = Join-Path $out $h.ToLowerInvariant()
            Write-Host "Publishing $h ..."
            $log = & dotnet publish (Join-Path $root "src/Squill.App.$h") -c Release -r win-x64 -o $dir "-t:Rebuild;Publish" 2>&1 | ForEach-Object { "$_" }
            if ($LASTEXITCODE -ne 0) {
                $log | Select-Object -Last 40 | Write-Host
                Write-Host "Publish of $h failed."
                exit 2
            }
            $found += Get-Warnings $log
        }
    }
    finally {
        & dotnet build-server shutdown | Out-Null
        if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    }
}

$bad = @($found | Where-Object { $_.IsOurs -or $baseline -notcontains $_.Key } | Group-Object Key)
foreach ($k in $baseline | Where-Object { $found.Key -notcontains $_ }) { Write-Host "stale baseline entry: $k" }
Write-Host "Baseline size: $($baseline.Count); warnings found: $($found.Count)"

if ($bad.Count -gt 0) {
    foreach ($g in $bad) { Write-Host "NEW WARNING $($g.Name)`n  $($g.Group[0].Line)" }
    exit 1
}
Write-Host 'OK: only baseline warnings.'
exit 0

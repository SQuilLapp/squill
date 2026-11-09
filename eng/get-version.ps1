<#
.SYNOPSIS
Prints SquillVersion from eng/Version.props; exits 1 when it is not valid SemVer 2 or when -Tag is not "v" + the version.
.PARAMETER Tag
Optional release tag (for example v0.1.0) that must equal "v" + the version.
#>
param([string]$Tag)

$ErrorActionPreference = 'Stop'
$props = Join-Path $PSScriptRoot 'Version.props'
$version = ([xml](Get-Content -Raw $props)).Project.PropertyGroup.SquillVersion
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    [Console]::Error.WriteLine("SquillVersion '$version' in eng/Version.props is not a valid SemVer 2 version.")
    exit 1
}
if ($PSBoundParameters.ContainsKey('Tag') -and $Tag -cne "v$version") {
    [Console]::Error.WriteLine("Tag '$Tag' does not match eng/Version.props: expected 'v$version'.")
    exit 1
}
$version

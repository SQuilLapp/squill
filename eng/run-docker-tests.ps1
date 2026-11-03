#requires -Version 7
<#
.SYNOPSIS
Runs the Docker-gated driver tests (Testcontainers) on Podman.
.PARAMETER Engine
postgres, mysql, sqlserver or all (default).
.PARAMETER Filter
Extra arguments passed to the test application, split on spaces, for example '--filter-method *ConnectionString*'.
#>
param(
    [ValidateSet('postgres', 'mysql', 'sqlserver', 'all')][string]$Engine = 'all',
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projects = @{ postgres = 'Postgres'; mysql = 'MySql'; sqlserver = 'SqlServer' }
$engines = $Engine -eq 'all' ? @('postgres', 'mysql', 'sqlserver') : @($Engine)

if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('podman was not found on PATH. Install Podman 5.x (https://podman.io) and retry.')
    exit 1
}

function Get-Machine { (podman machine inspect | ConvertFrom-Json)[0] }

$machine = Get-Machine
if ($machine.State -ne 'running') {
    podman machine start
    if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine('podman machine start failed.'); exit 1 }
    $machine = Get-Machine
}

if ($engines -contains 'sqlserver' -and $machine.Resources.Memory -lt 4096) {
    [Console]::Error.WriteLine("The Podman machine has $($machine.Resources.Memory) MiB; SQL Server needs 4096. Run: podman machine stop; podman machine set --memory 4096; podman machine start")
    exit 1
}

# Keep a DOCKER_HOST the caller already set (Docker Desktop or custom). Otherwise \.\pipe\x becomes npipe://./pipe/x (Docker.DotNet rejects the four-slash form).
if (-not $env:DOCKER_HOST) {
    $env:DOCKER_HOST = 'npipe:' + $machine.ConnectionInfo.PodmanPipe.Path.Replace([string][char]92, '/')
}
$env:TESTCONTAINERS_RYUK_DISABLED = 'true'
$env:SQUILL_DOCKER_TESTS = '1'

$extra = $Filter ? ($Filter -split '\s+' | Where-Object { $_ }) : @()
$exit = 0
try {
    foreach ($e in $engines) {
        dotnet test --project (Join-Path $root "tests/Squill.Data.$($projects[$e]).Tests") -c Release @extra
        if ($LASTEXITCODE -ne 0) { $exit = $LASTEXITCODE }
    }
}
finally {
    # Ryuk is disabled, so nothing else reaps the containers.
    $left = podman ps -aq --filter label=org.testcontainers=true
    if ($left) { podman rm -f @left | Out-Null }
}
exit $exit

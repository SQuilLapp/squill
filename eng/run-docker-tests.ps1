#requires -Version 7
<#
.SYNOPSIS
Runs the Docker-gated driver tests (Testcontainers) on Podman.
.PARAMETER Engine
postgres, mysql, sqlserver, ssh (the SSH tunnel tests of the Platform and Presentation projects, on PostgreSQL), oracle (a large image, so not part of all) or all (default).
.PARAMETER Keep
Instead of running tests, starts the container of one engine (postgres, mysql or sqlserver), loads tests/Shared/SampleSchema/<engine>.sql, prints host, port, user and password, and waits for Enter before removing it. For driving the app by hand.
.PARAMETER Filter
Extra arguments passed to the test application, split on spaces, for example '--filter-method *ConnectionString*'.
#>
param(
    [ValidateSet('postgres', 'mysql', 'sqlserver', 'ssh', 'oracle', 'all')][string]$Engine = 'all',
    [string]$Filter = '',
    [switch]$Keep
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projects = @{ postgres = 'Postgres'; mysql = 'MySql'; sqlserver = 'SqlServer'; oracle = 'Oracle' }
$engines = $Engine -eq 'all' ? @('postgres', 'mysql', 'sqlserver', 'ssh') : @($Engine)

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

# A WSL machine is not limited by podman's Memory value (the VM takes what WSL grants, see .wslconfig), and podman machine set --memory is refused for it.
$isWsl = $machine.ConfigDir.Path -match '[\/]wsl$'
if ($engines -contains 'sqlserver' -and -not $isWsl -and $machine.Resources.Memory -lt 4096) {
    [Console]::Error.WriteLine("The Podman machine has $($machine.Resources.Memory) MiB; SQL Server needs 4096. Run: podman machine stop; podman machine set --memory 4096; podman machine start")
    exit 1
}

# Keep a DOCKER_HOST the caller already set (Docker Desktop or custom). Otherwise \.\pipe\x becomes npipe://./pipe/x (Docker.DotNet rejects the four-slash form).
if (-not $env:DOCKER_HOST) {
    $env:DOCKER_HOST = 'npipe:' + $machine.ConnectionInfo.PodmanPipe.Path.Replace([string][char]92, '/')
}
$env:TESTCONTAINERS_RYUK_DISABLED = 'true'
$env:SQUILL_DOCKER_TESTS = '1'

if ($Keep) {
    # Fixed throw-away credentials, same as the test fixtures; the port is published on 127.0.0.1 only.
    $specs = @{
        postgres  = @{ Image = 'postgres:17-alpine'; Port = 5432; Env = @('POSTGRES_PASSWORD=squill-test'); User = 'postgres'; Password = 'squill-test'; Db = 'postgres'
            Load = { param($n) Get-Content -Raw (Join-Path $root 'tests/Shared/SampleSchema/postgres.sql') | podman exec -i $n psql -U postgres -v ON_ERROR_STOP=1 } }
        mysql     = @{ Image = 'mysql:8.4'; Port = 3306; Env = @('MYSQL_ROOT_PASSWORD=squill-test', 'MYSQL_DATABASE=squill'); User = 'root'; Password = 'squill-test'; Db = 'squill'
            Load = { param($n) Get-Content -Raw (Join-Path $root 'tests/Shared/SampleSchema/mysql.sql') | podman exec -i $n mysql -uroot -psquill-test squill } }
        sqlserver = @{ Image = 'mcr.microsoft.com/mssql/server:2022-latest'; Port = 1433; Env = @('ACCEPT_EULA=Y', 'MSSQL_SA_PASSWORD=Squill-test1'); User = 'sa'; Password = 'Squill-test1'; Db = 'master'
            Load = { param($n) Get-Content -Raw (Join-Path $root 'tests/Shared/SampleSchema/sqlserver.sql') | podman exec -i $n /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P Squill-test1 -b } }
    }
    if (-not $specs.ContainsKey($Engine)) { [Console]::Error.WriteLine('-Keep needs -Engine postgres, mysql or sqlserver.'); exit 1 }
    $spec = $specs[$Engine]
    $name = "squill-keep-$Engine"
    podman rm -f $name 2>$null | Out-Null
    try {
        $envArgs = $spec.Env | ForEach-Object { '-e'; $_ }
        podman run -d --name $name --label org.testcontainers=true -p "127.0.0.1::$($spec.Port)" @envArgs $spec.Image | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "podman run failed for $($spec.Image)" }
        $hostPort = (podman port $name "$($spec.Port)/tcp" | Select-Object -First 1).Split(':')[-1]
        for ($i = 0; $i -lt 90; $i++) {
            Start-Sleep -Seconds 2
            & $spec.Load $name *> $null
            if ($LASTEXITCODE -eq 0) { break }
        }
        if ($LASTEXITCODE -ne 0) { throw 'the server did not accept the sample schema within 3 minutes' }
        Write-Host "$Engine ready: host 127.0.0.1  port $hostPort  user $($spec.User)  password $($spec.Password)  database $($spec.Db)"
        Read-Host 'Press Enter to stop and remove the container' | Out-Null
    }
    finally { podman rm -f $name 2>$null | Out-Null }
    exit 0
}

$extra = $Filter ? ($Filter -split '\s+' | Where-Object { $_ }) : @()
$exit = 0
try {
    foreach ($e in $engines) {
        $runs = $e -eq 'ssh' ? @(@('Squill.Platform.Tests', '--filter-class', '*SshJumpHostTests'), @('Squill.Presentation.Tests', '--filter-class', '*SshSessionTests')) : @(, @("Squill.Data.$($projects[$e]).Tests"))
        foreach ($run in $runs) {
            dotnet test --project (Join-Path $root "tests/$($run[0])") -c Release @($run | Select-Object -Skip 1) @extra
            if ($LASTEXITCODE -ne 0) { $exit = $LASTEXITCODE }
        }
    }
}
finally {
    # Ryuk is disabled, so nothing else reaps the containers.
    $left = podman ps -aq --filter label=org.testcontainers=true
    if ($left) { podman rm -f @left | Out-Null }
}
exit $exit

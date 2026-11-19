#requires -Version 7
<# Starts (or stops) a PostgreSQL container with the data of the verification passes; see tools/visual/README.md.
   Start prints how to connect; -Dir also writes the profile pg-visual into <Dir>/profiles.json.
   Exit codes: 0 ok, 1 failure, 3 Podman missing. #>
param(
    [Parameter(Mandatory)][ValidateSet('Start', 'Stop', 'Status')][string]$Action,
    [int]$Port = 55432,
    [string]$Name = 'squill-visual-pg',
    [string]$Dir
)
$ErrorActionPreference = 'Stop'
$password = 'squill-test'

if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Podman is not installed or not on PATH.')
    exit 3
}

function Wait-Until([scriptblock]$Test, [int]$Seconds, [string]$What) {
    $deadline = [datetime]::UtcNow.AddSeconds($Seconds)
    while ([datetime]::UtcNow -lt $deadline) {
        if (& $Test) { return }
        Start-Sleep -Seconds 2
    }
    [Console]::Error.WriteLine("Timed out after $Seconds s waiting for $What.")
    exit 1
}

function Test-Machine { podman info 2>$null | Out-Null; $LASTEXITCODE -eq 0 }

if ($Action -eq 'Status') {
    podman ps -a --filter "name=$Name" --format '{{.Names}} {{.Status}} {{.Ports}}'
    return
}
if ($Action -eq 'Stop') {
    if (Test-Machine) {
        podman rm -f $Name 2>$null | Out-Null
        $left = @(podman ps -a --format '{{.Names}}').Count
        Write-Host "Removed $Name. podman ps -a lists $left container(s)."
    } else { Write-Host 'Podman machine is not running: nothing to remove.' }
    return
}

# Start
if (-not (Test-Machine)) {
    Write-Host 'Starting the Podman machine...'
    podman machine start 2>&1 | Out-Host
    Wait-Until { Test-Machine } 180 'the Podman machine'
}
# Same image as eng/run-docker-tests.ps1.
$image = 'postgres:17-alpine'
podman rm -f $Name 2>$null | Out-Null
podman run -d --name $Name -p "${Port}:5432" -e "POSTGRES_PASSWORD=$password" $image -c max_connections=600 | Out-Null
if ($LASTEXITCODE) { [Console]::Error.WriteLine('podman run failed.'); exit 1 }
# pg_isready answers during the init restart too: wait for a real query over TCP, which only the final server serves.
Wait-Until { (podman exec $Name psql -U postgres -h 127.0.0.1 -tAc 'select 1' 2>$null) -eq '1' } 120 'PostgreSQL'

Get-Content -Raw -Encoding utf8 (Join-Path $PSScriptRoot 'postgres-scenario.sql') |
    podman exec -i $Name psql -U postgres -h 127.0.0.1 -v ON_ERROR_STOP=1 -q | Out-Null
if ($LASTEXITCODE) { [Console]::Error.WriteLine('Loading postgres-scenario.sql failed.'); exit 1 }

[pscustomobject]@{ Host = '127.0.0.1'; Port = $Port; User = 'postgres'; Password = $password; Database = 'postgres' } | Format-Table | Out-String | Write-Host

if ($Dir) {
    $null = New-Item -ItemType Directory -Force $Dir
    $file = Join-Path $Dir 'profiles.json'
    $id = [guid]::new([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes('squill-visual-pg-visual'))).ToString()
    $profile = [ordered]@{ id = $id; name = 'pg-visual'; dialect = 'PostgreSql'; host = '127.0.0.1'; port = $Port; database = 'postgres'
        userName = 'postgres'; filePath = $null; ssh = $null; environment = 'Staging' }
    $all = @(if (Test-Path $file) { Get-Content -Raw $file | ConvertFrom-Json -AsHashtable | Where-Object { $_.name -ne 'pg-visual' } })
    ConvertTo-Json -InputObject @($all + $profile) -Depth 4 | Set-Content $file -Encoding utf8
    Write-Host "Profile pg-visual written to $file (the password is not stored: type $password at the first connection)."
}

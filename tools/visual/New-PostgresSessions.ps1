#requires -Version 7
<# Opens idle PostgreSQL sessions and, with -Block, a blocking chain of depth 2 in the container of New-PostgresScenario.ps1.
   -Stop terminates them. Uses the container's psql through podman exec: no client needed on the host. #>
param(
    [string]$Name = 'squill-visual-pg',
    [int]$Count = 450,
    [switch]$Block,
    [switch]$Stop
)
$ErrorActionPreference = 'Stop'
function Invoke-Psql([string]$Sql) { podman exec $Name psql -U postgres -h 127.0.0.1 -tAc $Sql }

if ($Stop) {
    Invoke-Psql "select count(pg_terminate_backend(pid)) from pg_stat_activity where application_name like 'squill-visual%'" | Out-Null
    podman exec $Name sh -c 'pkill -f squill-visual 2>/dev/null; true' | Out-Null
    Write-Host 'Visual sessions stopped.'
    return
}

# One background shell in the container opens $Count psql clients that sleep.
$idle = "for i in `$(seq 1 $Count); do PGAPPNAME=squill-visual psql -U postgres -h 127.0.0.1 -qAt -c 'select pg_sleep(3600)' >/dev/null 2>&1 & done; wait"
podman exec -d $Name sh -c $idle
if ($Block) {
    # Holder: updates a row and keeps its transaction open; waiter: updates the same row.
    $holder = "PGAPPNAME=squill-visual-holder psql -U postgres -h 127.0.0.1 -qAt -c ""begin; update ventes.clients set ville = ville where id = 10482; select pg_sleep(3600)"" >/dev/null 2>&1"
    podman exec -d $Name sh -c $holder
    Start-Sleep -Seconds 2
    $waiter = "PGAPPNAME=squill-visual-waiter psql -U postgres -h 127.0.0.1 -qAt -c ""update ventes.clients set ville = ville where id = 10482"" >/dev/null 2>&1"
    podman exec -d $Name sh -c $waiter
}
Start-Sleep -Seconds 8
Write-Host "Sessions named squill-visual*: $(Invoke-Psql "select count(*) from pg_stat_activity where application_name like 'squill-visual%'")"
Invoke-Psql "select pid, application_name, wait_event_type from pg_stat_activity where application_name like 'squill-visual-%' order by 2"
Write-Host 'Stop them with: pwsh tools/visual/New-PostgresSessions.ps1 -Stop'

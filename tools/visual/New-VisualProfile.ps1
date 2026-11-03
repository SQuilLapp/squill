#requires -Version 7
<# Creates the sample SQLite database and a profile store in -Dir; run the app with SQUILL_DATA_DIR=<Dir>. Windows only. #>
param([Parameter(Mandatory)][string]$Dir)
$ErrorActionPreference = 'Stop'
$null = New-Item -ItemType Directory -Force $Dir
$Dir = (Resolve-Path $Dir).Path
$db = Join-Path $Dir 'ventes.db'
$seed = Join-Path $PSScriptRoot 'seed.sql'
Remove-Item $db -ErrorAction SilentlyContinue
if (Get-Command sqlite3 -ErrorAction SilentlyContinue) {
    Get-Content $seed -Raw -Encoding utf8 | sqlite3 $db
    if ($LASTEXITCODE) { throw 'sqlite3 failed' }
    $used = 'sqlite3 CLI'
} else {
    dotnet run (Join-Path $PSScriptRoot 'Seed.cs') -- $db $seed
    if ($LASTEXITCODE) { throw 'Seed.cs failed' }
    $used = 'Seed.cs'
}
$profiles = @(@{
    id = [guid]::NewGuid().ToString(); name = 'pg-prod-eu'; dialect = 'Sqlite'
    host = $null; port = $null; database = $null; userName = $null; filePath = $db; ssh = $null
})
ConvertTo-Json -InputObject $profiles -Depth 4 | Set-Content (Join-Path $Dir 'profiles.json') -Encoding utf8
Write-Host "Created $db with $used and profiles.json in $Dir"

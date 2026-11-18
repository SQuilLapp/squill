#requires -Version 7
<# Creates the sample SQLite database and a profile store in -Dir; run the app with SQUILL_DATA_DIR=<Dir>. Windows only.
   -Scenario Full also prepares the data of docs/verification/new-screens.md (M01, M09, M10); see tools/visual/README.md. #>
param(
    [Parameter(Mandatory)][string]$Dir,
    [ValidateSet('Basic', 'Full')][string]$Scenario = 'Basic',
    [ValidateSet('Light', 'Dark')][string]$Theme
)
$ErrorActionPreference = 'Stop'
$null = New-Item -ItemType Directory -Force $Dir
$Dir = (Resolve-Path $Dir).Path
$seed = Join-Path $PSScriptRoot 'seed.sql'
$haveCli = [bool](Get-Command sqlite3 -ErrorAction SilentlyContinue)
$used = $haveCli ? 'sqlite3 CLI' : 'Seed.cs'

# Builds $db from the SQL text (recreated from scratch).
function New-Database([string]$db, [string]$sql) {
    Remove-Item $db -ErrorAction SilentlyContinue
    if ($haveCli) {
        $sql | sqlite3 $db
        if ($LASTEXITCODE) { throw 'sqlite3 failed' }
    } else {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) "squill-$([guid]::NewGuid()).sql"
        try {
            Set-Content $tmp $sql -Encoding utf8
            dotnet run (Join-Path $PSScriptRoot 'Seed.cs') -- $db $tmp
            if ($LASTEXITCODE) { throw 'Seed.cs failed' }
        } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
    }
}

if ($Scenario -eq 'Basic') {
    $db = Join-Path $Dir 'ventes.db'
    New-Database $db (Get-Content $seed -Raw -Encoding utf8)
    $profiles = @(@{
        id = [guid]::NewGuid().ToString(); name = 'pg-prod-eu'; dialect = 'Sqlite'
        host = $null; port = $null; database = $null; userName = $null; filePath = $db; ssh = $null
    })
    ConvertTo-Json -InputObject $profiles -Depth 4 | Set-Content (Join-Path $Dir 'profiles.json') -Encoding utf8
    Write-Host "Created $db with $used and profiles.json in $Dir"
    return
}

# --- Full scenario: deterministic ids, so a second run writes identical profiles. ---
$seedSql = Get-Content $seed -Raw -Encoding utf8
$blocks = @{}
$current = $null
foreach ($line in Get-Content (Join-Path $PSScriptRoot 'scenario.sql') -Encoding utf8) {
    if ($line -match '^-- @db (\w+)\s*$') { $current = $Matches[1]; $blocks[$current] = ''; continue }
    if ($current) { $blocks[$current] += $line + "`n" }
}

function New-ProfileId([string]$name) {
    [guid]::new([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes("squill-visual-$name"))).ToString()
}

$rows = [System.Collections.Generic.List[object]]::new()
function Add-Profile([string]$name, [string]$environment, [string]$purpose, [string]$file = "$name.db") {
    $path = Join-Path $Dir $file
    $rows.Add([pscustomobject]@{ File = $file; Profile = $name; Environment = $environment; Purpose = $purpose; Path = $path; Id = New-ProfileId $name })
}

# M01: six profiles of the four environments, with different last-opened times.
foreach ($p in @(
    @('pg-prod-eu', 'Production'), @('mysql-staging', 'Staging'), @('sqlserver-dev', 'Development'),
    @('local-sqlite', 'Local'), @('pg-staging-us', 'Staging'), @('sqlserver-prod', 'Production'))) {
    Add-Profile $p[0] $p[1] 'M01'
    New-Database (Join-Path $Dir "$($p[0]).db") $seedSql
}
$recent = @($rows | Where-Object Purpose -eq 'M01')

# M09: two SQLite files with differences.
Add-Profile 'cmp-source' 'Staging' 'M09' 'cmp_source.db'
New-Database (Join-Path $Dir 'cmp_source.db') ($seedSql + "`n" + $blocks['cmp_source'])
Add-Profile 'cmp-target' 'Production' 'M09' 'cmp_target.db'
New-Database (Join-Path $Dir 'cmp_target.db') ($seedSql + "`n" + $blocks['cmp_target'])

# M10: an empty people table and the CSV to import into it.
Add-Profile 'import-target' 'Local' 'M10' 'import_target.db'
New-Database (Join-Path $Dir 'import_target.db') $blocks['import_target']

$names = 'Éloïse', 'Zoé', 'Hélène', 'François', 'Noël', 'Céline', 'Amélie', 'Étienne'
$csv = [System.Collections.Generic.List[string]]::new()
$csv.Add('id;nom;naissance;montant')
for ($i = 1; $i -le 40; $i++) {
    $nom = "$($names[$i % $names.Count]) Lefèvre"
    $date = [datetime]'1980-01-15'
    $date = $date.AddDays($i * 173).ToString('yyyy-MM-dd')
    $montant = ($i * 37.5).ToString('0.00', [cultureinfo]::InvariantCulture)
    $id = "$i"
    if ($i -eq 7) { $nom = '"Dupont; Fils & Associés"' }
    if ($i -eq 12) { $id = 'douze' }          # text in an integer column
    if ($i -eq 25) { $date = '2024-02-31' }   # impossible date
    if ($i -eq 33) { $nom = '' }              # empty required field
    $csv.Add("$id;$nom;$date;$montant")
}
[IO.File]::WriteAllText((Join-Path $Dir 'people.csv'), ($csv -join "`n") + "`n", [Text.UTF8Encoding]::new($false))

$profiles = @($rows | ForEach-Object {
    [ordered]@{ id = $_.Id; name = $_.Profile; dialect = 'Sqlite'; host = $null; port = $null; database = $null; userName = $null
       filePath = $_.Path; ssh = $null; environment = $_.Environment }
})
ConvertTo-Json -InputObject $profiles -Depth 4 | Set-Content (Join-Path $Dir 'profiles.json') -Encoding utf8

$now = [DateTimeOffset]::Now
$offsets = @(5, 90, (60 * 26), (60 * 24 * 3), (60 * 24 * 9), (60 * 24 * 40))   # minutes ago, newest first
$recentJson = @(for ($i = 0; $i -lt $recent.Count; $i++) {
    [ordered]@{ profileId = $recent[$i].Id; lastOpened = $now.AddMinutes(-$offsets[$i]).ToString('o') }
})
ConvertTo-Json -InputObject $recentJson -Depth 3 | Set-Content (Join-Path $Dir 'recent.json') -Encoding utf8

Remove-Item (Join-Path $Dir 'settings.json') -ErrorAction SilentlyContinue
if ($Theme) {
    $settings = "[ { `"theme`": `"$Theme`", `"editorFontSize`": 13, `"maxGridRows`": 1000000, `"formatterKeywordCase`": `"Upper`" } ]"
    Set-Content (Join-Path $Dir 'settings.json') $settings -Encoding utf8
}

$rows | Select-Object File, Profile, Environment, Purpose | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "people.csv (M10: 40 rows, 3 fail conversion), recent.json and profiles.json written in $Dir (databases built with $used)"

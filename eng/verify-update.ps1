<#
.SYNOPSIS
End-to-end check of the installer and of the update flow on this machine: pack 0.0.1 and 0.0.2, install 0.0.1 silently,
let the app update itself from a local feed, uninstall, and prove that the data directory survives. Windows only, no network.
.DESCRIPTION
Isolation: pack id SQuilLVerify (install root %LOCALAPPDATA%\SQuilLVerify), data directory <Scratch>\data (SQUILL_DATA_DIR),
feed <Scratch>\feed (SQUILL_UPDATE_FEED). The app is started by this script with those variables; the Start menu / desktop shortcut
the installer creates does not carry them. The window is driven through UI Automation (button names from the English catalog,
SQUILL_LANGUAGE=en). A step that cannot be driven is reported as "not driven", never as ok.
The script refuses to run when a SQuilL shortcut already exists (the installer would overwrite it, the uninstaller would delete it),
and it never touches processes it did not start. The pack takes several minutes (two AOT publishes).
.PARAMETER Scratch
Working directory (feed, data, installers). Default: artifacts/verify. Removed at the end unless -KeepInstalled.
.PARAMETER KeepInstalled
Leaves the installed app, the scratch folder and the data in place (for a manual check); uninstall by hand afterwards.
.PARAMETER SelfTest
Checks the result table and the shortcut guard without packing or installing anything.
#>
param(
    [string]$Scratch,
    [switch]$KeepInstalled,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$packId = 'SQuilLVerify'
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Step, [string]$Result, [string]$Note = '') {
    $results.Add([pscustomobject]@{ Step = $Step; Result = $Result; Note = $Note })
    "[$Result] $Step $Note"
}

function Get-ShortcutFiles {
    $dirs = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('CommonDesktopDirectory'),
        (Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'), (Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) 'Programs'))
    foreach ($dir in $dirs) { if ($dir -and (Test-Path $dir)) { Get-ChildItem $dir -Filter 'SQuilL*.lnk' -ErrorAction SilentlyContinue } }
}

if ($SelfTest) {
    Add-Result 'a' 'ok'
    Add-Result 'b' 'not driven (why)' 'note'
    if ($results.Count -ne 2 -or $results[1].Result -ne 'not driven (why)') { throw 'Self-test: the result table is wrong.' }
    $null = @(Get-ShortcutFiles)
    'Self-test passed.'
    exit 0
}

if ($env:SQUILL_DATA_DIR -or $env:SQUILL_UPDATE_FEED) { throw 'Unset SQUILL_DATA_DIR and SQUILL_UPDATE_FEED first: this script sets them itself.' }
$root = Split-Path -Parent $PSScriptRoot
if (-not $Scratch) { $Scratch = Join-Path $root 'artifacts/verify' }
$Scratch = [IO.Path]::GetFullPath($Scratch, $root)
$feed = Join-Path $Scratch 'feed'
$data = Join-Path $Scratch 'data'
$installRoot = Join-Path $env:LOCALAPPDATA $packId
$appExe = Join-Path $installRoot 'current\Squill.App.WinUI.exe'
if (Test-Path $installRoot) { throw "$installRoot already exists: uninstall it first." }
if (@(Get-ShortcutFiles).Count -gt 0) { throw 'A SQuilL shortcut already exists (desktop or Start menu): it would be overwritten and then deleted. Remove it first.' }

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# Processes of the verification install (matched by path, so the owner's own SQuilL is never touched).
function Get-VerifyProcess { Get-Process -Name 'Squill.App.WinUI' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$installRoot\*" } }

function Find-Ui([string]$Name, [int]$TimeoutSeconds = 15, [switch]$Contains) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($p in @(Get-VerifyProcess)) {
            $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
                [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id))
            foreach ($w in $windows) {
                $hit = $w.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) |
                    Where-Object { $n = $_.Current.Name; $Contains ? $n -like "*$Name*" : $n -ceq $Name } | Select-Object -First 1
                if ($hit) { return $hit }
            }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    $null
}

function Invoke-Ui($Element) { $Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }

function Start-App {
    Start-Process $appExe -PassThru
}

function Wait-AppExit([int]$ProcessId, [int]$Seconds = 30) {
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($p) { [void]$p.WaitForExit($Seconds * 1000) }
}

# Closes the window of every verify process and waits; forces the exit only when it ignores the request.
function Stop-App {
    foreach ($p in @(Get-VerifyProcess)) {
        [void]$p.CloseMainWindow()
        if (-not $p.WaitForExit(10000)) { Stop-Process -Id $p.Id -Force }
    }
}

function Get-Hashes([string[]]$Names) { $Names | ForEach-Object { (Get-FileHash (Join-Path $data $_) -Algorithm SHA256).Hash } }

$env:SQUILL_DATA_DIR = $data
$env:SQUILL_UPDATE_FEED = $feed
$env:SQUILL_LANGUAGE = 'en'
$installed = $false
try {
    Remove-Item $Scratch -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory $feed, $data | Out-Null

    # 1. Pack 0.0.1, keep its Setup (the 0.0.2 pack replaces the version-less file name), then 0.0.2 into the same feed for the delta.
    $pack = Join-Path $PSScriptRoot 'pack-windows.ps1'
    & pwsh -NoProfile -File $pack -Version 0.0.1 -Channel stable -PackId $packId -AllowVersionOverride -OutputDir $feed
    if ($LASTEXITCODE -ne 0) { throw 'Packing 0.0.1 failed.' }
    $setup1 = Join-Path $Scratch 'Setup-0.0.1.exe'
    Copy-Item (Join-Path $feed "$packId-stable-x64-Setup.exe") $setup1
    # pack-windows.ps1 empties its output directory when it publishes, so 0.0.2 is published here and packed with -SkipPublish.
    $publish = Join-Path $root 'artifacts/publish'
    Remove-Item $publish -Recurse -Force
    & dotnet publish (Join-Path $root 'src/Squill.App.WinUI') -c Release -r win-x64 -o $publish '-p:Version=0.0.2' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Publishing 0.0.2 failed.' }
    & pwsh -NoProfile -File $pack -Version 0.0.2 -Channel stable -PackId $packId -AllowVersionOverride -OutputDir $feed -SkipPublish
    if ($LASTEXITCODE -ne 0) { throw 'Packing 0.0.2 failed.' }
    Add-Result '1 pack 0.0.1 and 0.0.2' 'ok'

    # 2. Feed content.
    $names = (Get-ChildItem $feed -File).Name
    $json = Get-Content -Raw (Join-Path $feed 'releases.stable-x64.json')
    $missing = @("$packId-stable-x64-Setup.exe", "$packId-0.0.1-stable-x64-full.nupkg", "$packId-0.0.2-stable-x64-full.nupkg", "$packId-0.0.2-stable-x64-delta.nupkg") | Where-Object { $_ -notin $names }
    if ($missing -or $json -notmatch '0\.0\.1' -or $json -notmatch '0\.0\.2') { throw "Feed incomplete: missing $($missing -join ', ')." }
    Add-Result '2 feed content (Setup, 2 full, 1 delta, json lists both)' 'ok'

    # 3. Data to preserve.
    Set-Content (Join-Path $data 'settings.json') '[{"theme":"System","editorFontSize":13,"maxGridRows":1000000,"formatterKeywordCase":"Upper","language":"English","updateFrequency":"Startup","updateChannel":"Stable"}]'
    Set-Content (Join-Path $data 'history.json') '[]'
    [IO.File]::WriteAllBytes((Join-Path $data 'marker.bin'), [byte[]](1..200 | ForEach-Object { $_ % 256 }))
    $before = Get-Hashes 'history.json', 'marker.bin'
    Add-Result '3 seed data directory (settings Startup, marker files)' 'ok'

    # 4. Silent install of 0.0.1.
    $installed = $true
    Start-Process $setup1 -ArgumentList '--silent' -Wait
    if (-not (Test-Path $appExe)) { throw "Setup did not create $appExe." }
    $v = (Get-Item $appExe).VersionInfo.ProductVersion
    if ($v -notlike '0.0.1*') { throw "Installed product version is '$v'." }
    if (-not (Test-Path (Join-Path $installRoot 'Update.exe'))) { throw 'Update.exe is missing from the install root.' }
    Add-Result '4 silent install, product version 0.0.1' 'ok' "($v)"

    # 5. Start 0.0.1, banner, download, restart.
    $first = Start-App
    $banner = Find-Ui 'SQuilL 0.0.2 is available' 25 -Contains
    $driven = $null -ne $banner
    $capture = Join-Path $PSScriptRoot '../tools/visual/Capture-Window.ps1'
    if ($driven) {
        & pwsh -NoProfile -File $capture -Process Squill.App.WinUI -Out (Join-Path $Scratch 'banner.png') | Out-Null
        Add-Result '5a banner "SQuilL 0.0.2 is available" within 25 s' 'ok'
        $download = Find-Ui 'Download' 5
        if ($download) { Invoke-Ui $download } else { throw 'The Download button was not found.' }
        $restart = Find-Ui 'Restart to update' 60
        if ($restart) { Add-Result '5b Download then Restart to update appears' 'ok' } else { throw 'Restart to update did not appear within 60 s.' }
        # The app exits while the click is being delivered, so UI Automation may report the element as gone.
        try { Invoke-Ui $restart } catch [System.Runtime.InteropServices.COMException] { $null = $_ }
        Wait-AppExit $first.Id 60
    }
    else { Add-Result '5 banner and restart click' 'not driven (UI Automation did not find the banner in 25 s)' 'to check by hand' }

    # 6. After the relaunch.
    if ($driven) {
        $deadline = [DateTime]::UtcNow.AddSeconds(90)
        while ([DateTime]::UtcNow -lt $deadline -and -not (Get-VerifyProcess)) { Start-Sleep -Seconds 1 }
        if (-not (Get-VerifyProcess)) { throw 'The app was not relaunched within 90 s.' }
    }
    else {
        # Not driven: apply the pending update through Update.exe is not what a user does, so the update is left unverified here.
        Stop-App
        Add-Result '6 relaunch on 0.0.2, About text, what is new' 'not driven (needs step 5)' 'to check by hand'
    }
    if ($driven) {
        $v2 = (Get-Item $appExe).VersionInfo.ProductVersion
        $stateFile = Join-Path $data 'install-state.json'
        $state = (Test-Path $stateFile) ? (Get-Content -Raw $stateFile) : ''
        $dataSame = (Get-Hashes 'history.json', 'marker.bin') -join ',' -eq ($before -join ',')
        if ($v2 -notlike '0.0.2*') { throw "After the update the product version is '$v2'." }
        if (-not $dataSame) { throw 'Seeded data changed during the update.' }
        if ($state -notmatch '0\.0\.2') { throw "install-state.json does not say 0.0.2: $state" }
        Add-Result '6a exe is 0.0.2, data byte-identical, install-state.json says 0.0.2' 'ok' "($v2)"
        $menu = Find-Ui 'Help' 15
        if ($menu) {
            try {
                $menu.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
                $about = Find-Ui 'About' 5
                Invoke-Ui $about
                $text = Find-Ui 'Version 0.0.2' 10
                $whatsNew = Find-Ui 'Updated from 0.0.1' 3
                if ($text -and $whatsNew) { Add-Result '6b About says "Version 0.0.2" and "Updated from 0.0.1"' 'ok' }
                else { Add-Result '6b About text' 'not driven (About window opened but the texts were not found)' 'to check by hand' }
                $close = Find-Ui 'Close' 3
                if ($close) { Invoke-Ui $close }
            }
            catch { Add-Result '6b About text' 'not driven (menu navigation failed)' "to check by hand: $($_.Exception.Message)" }
        }
        else { Add-Result '6b About text' 'not driven (Help menu not found)' 'to check by hand' }

        # 7 + 8. Second start: nothing newer, and no connection outside this machine while it runs.
        Stop-App
        $second = Start-App
        $remote = [System.Collections.Generic.HashSet[string]]::new()
        $local = '^(127\.|::1$|0\.0\.0\.0$|::$)'
        for ($i = 0; $i -lt 30; $i++) {
            foreach ($c in @(Get-NetTCPConnection -OwningProcess (Get-VerifyProcess).Id -ErrorAction SilentlyContinue)) {
                if ($c.RemoteAddress -notmatch $local -and $c.State -ne 'Listen') { [void]$remote.Add("$($c.RemoteAddress):$($c.RemotePort)") }
            }
            Start-Sleep -Seconds 1
        }
        if ($remote.Count -gt 0) { throw "The app connected outside this machine: $($remote -join ', ')." }
        Add-Result '7 no remote connection during 30 s of a start with Startup checks' 'ok'
        $stillOffered = Find-Ui 'is available' 2 -Contains
        if ($stillOffered) { throw 'A second start still offers an update.' }
        Add-Result '8 second start: no banner' 'ok'
        Stop-App
    }
    else { Add-Result '7 no remote connection' 'not driven (needs step 6)'; Add-Result '8 second start finds nothing newer' 'not driven (needs step 6)' }

    # 9. Uninstall, data kept.
    Stop-App
    Start-Process (Join-Path $installRoot 'Update.exe') -ArgumentList 'uninstall', '--silent' -Wait
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline -and (Test-Path $installRoot)) { Start-Sleep -Seconds 1 }
    if (Test-Path $installRoot) { throw "$installRoot still exists 60 s after the uninstall." }
    $installed = $false
    if (@(Get-ShortcutFiles).Count -gt 0) { throw 'A SQuilL shortcut is left after the uninstall.' }
    if (-not (Test-Path (Join-Path $data 'marker.bin')) -or (Get-Hashes 'history.json', 'marker.bin') -join ',' -ne ($before -join ',')) { throw 'The data directory did not survive the uninstall.' }
    Add-Result '9 uninstall: install root and shortcuts gone, data directory intact' 'ok'
}
catch {
    Add-Result 'FAILED' 'bug' $_.Exception.Message
}
finally {
    Stop-App
    if ($installed -and -not $KeepInstalled -and (Test-Path (Join-Path $installRoot 'Update.exe'))) {
        Start-Process (Join-Path $installRoot 'Update.exe') -ArgumentList 'uninstall', '--silent' -Wait
        Start-Sleep -Seconds 5
    }
    Remove-Item Env:SQUILL_DATA_DIR, Env:SQUILL_UPDATE_FEED, Env:SQUILL_LANGUAGE -ErrorAction SilentlyContinue
    & dotnet build-server shutdown | Out-Null
    $left = @(Get-VerifyProcess)
    if (-not $KeepInstalled) {
        Remove-Item $Scratch -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $root 'artifacts/publish'), (Join-Path $root 'artifacts/releases') -Recurse -Force -ErrorAction SilentlyContinue
    }
    $clean = $left.Count -eq 0 -and ($KeepInstalled -or (-not (Test-Path $installRoot) -and @(Get-ShortcutFiles).Count -eq 0))
    Add-Result '10 cleanup: no process, install root and shortcuts gone' ($clean ? 'ok' : 'bug') ($clean ? '' : "left: $($left.Id -join ',')")
}

$results | Format-Table -AutoSize -Wrap | Out-String -Width 200
if ($results | Where-Object { $_.Result -eq 'bug' }) { exit 1 }

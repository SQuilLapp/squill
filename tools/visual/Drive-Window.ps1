#requires -Version 7
<# Drives the main window of a process (or one of its dialogs) with UI Automation, the mouse and the keyboard. Windows only.
   Actions: Dump, Click, Key, Type, Wait, Hover, Focus-Dialog, SelfTest. Exit codes: 0 ok, 1 window missing, 2 cannot get focus,
   4 element missing, 5 timeout, 6 bad arguments / self-test failure. See tools/visual/README.md. #>
param(
    [Parameter(Mandatory)][ValidateSet('Dump', 'Click', 'Key', 'Type', 'Wait', 'Hover', 'Focus-Dialog', 'SelfTest')][string]$Action,
    [string]$Process,
    [string]$Name,
    [string]$ControlType,
    [int]$Index = 0,
    [ValidateSet('Invoke', 'Mouse', 'DoubleClick', 'RightClick')][string]$Mode = 'Invoke',
    [string]$Keys,
    [string]$Text,
    [int]$Depth = 6,
    [string]$Filter,
    [int]$TimeoutSeconds = 10,
    [switch]$Gone,
    [int]$X = -1,
    [int]$Y = -1)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class SqDrv {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    /// <summary>Visible top-level windows of a process (owned or not).</summary>
    public static List<IntPtr> TopLevel(uint pid) {
        var r = new List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero);
        return r;
    }
    static INPUT Key(ushort vk, ushort scan, uint flags) {
        var i = new INPUT { type = 1 }; i.u.ki = new KEYBDINPUT { vk = vk, scan = scan, flags = flags }; return i;
    }
    /// <summary>Presses the virtual keys in order, then releases them in reverse order.</summary>
    public static void Chord(ushort[] vks) {
        var l = new List<INPUT>();
        foreach (var v in vks) l.Add(Key(v, 0, 0));
        for (int i = vks.Length - 1; i >= 0; i--) l.Add(Key(vks[i], 0, 2));
        SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
    }
    /// <summary>Types each UTF-16 unit with KEYEVENTF_UNICODE (0x4).</summary>
    public static void TypeText(string s) {
        var l = new List<INPUT>();
        foreach (var c in s) { l.Add(Key(0, c, 4)); l.Add(Key(0, c, 6)); }
        SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
    }
}
'@
[void][SqDrv]::SetProcessDPIAware()
$targetFile = Join-Path ([IO.Path]::GetTempPath()) 'squill-drive-target.txt'

function Stop-Driver([int]$Code, [string]$Message) { Write-Output "error: $Message"; exit $Code }

function Get-MainProcess {
    $p = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { Stop-Driver 1 "no window found for process '$Process'" }
    $p
}

# The target window: the dialog chosen by Focus-Dialog while it still exists, else the main window.
function Get-Target {
    $p = Get-MainProcess
    if (Test-Path $targetFile) {
        $h = [IntPtr][long](Get-Content $targetFile -Raw).Trim()
        $pid2 = [uint32]0; [void][SqDrv]::GetWindowThreadProcessId($h, [ref]$pid2)
        if ($pid2 -eq $p.Id -and [SqDrv]::IsWindowVisible($h)) { return $h }
        Remove-Item $targetFile -Force
    }
    $p.MainWindowHandle
}

# Keys and mouse events go to whatever is in front: refuse unless the target process owns the foreground window.
function Confirm-Foreground([IntPtr]$h) {
    $owner = [uint32]0; [void][SqDrv]::GetWindowThreadProcessId($h, [ref]$owner)
    function Test-Front { $f = [uint32]0; [void][SqDrv]::GetWindowThreadProcessId([SqDrv]::GetForegroundWindow(), [ref]$f); $f -eq $owner }
    if (Test-Front) { return }
    if ([SqDrv]::IsIconic($h)) { [void][SqDrv]::ShowWindow($h, 9) }
    $fg = [SqDrv]::GetForegroundWindow(); $fgPid = [uint32]0
    $fgThread = [SqDrv]::GetWindowThreadProcessId($fg, [ref]$fgPid); $me = [SqDrv]::GetCurrentThreadId()
    [void][SqDrv]::AttachThreadInput($me, $fgThread, $true)
    [void][SqDrv]::BringWindowToTop($h); [void][SqDrv]::SetForegroundWindow($h)
    [void][SqDrv]::AttachThreadInput($me, $fgThread, $false)
    Start-Sleep -Milliseconds 200
    if (-not (Test-Front)) {
        $f = [uint32]0; [void][SqDrv]::GetWindowThreadProcessId([SqDrv]::GetForegroundWindow(), [ref]$f)
        Stop-Driver 2 "window of '$Process' cannot get focus (foreground belongs to '$((Get-Process -Id $f -ErrorAction SilentlyContinue).ProcessName)')"
    }
}

function Get-Root { [System.Windows.Automation.AutomationElement]::FromHandle((Get-Target)) }

function Get-Matches([string]$Query, [string]$Type) {
    $all = (Get-Root).FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $list = foreach ($e in $all) {
        $c = $e.Current
        if ($Type -and $c.ControlType.ProgrammaticName -ne "ControlType.$Type") { continue }
        [pscustomobject]@{ E = $e; Name = $c.Name }
    }
    $exact = @($list | Where-Object { $_.Name -eq $Query })
    if ($exact.Count) { return $exact }
    @($list | Where-Object { $_.Name -and $_.Name.Contains($Query, [StringComparison]::OrdinalIgnoreCase) })
}

function Find-Element([string]$Query, [string]$Type, [int]$At) {
    $m = @(Get-Matches $Query $Type)
    if ($m.Count -gt $At) { return $m[$At].E }
    $null
}

function Stop-Missing([string]$Query) {
    $near = (Get-Root).FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        ForEach-Object { $_.Current.Name } | Where-Object { $_ } |
        Where-Object { $_.Contains($Query.Substring(0, [Math]::Min(3, $Query.Length)), [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -Unique -First 8
    Stop-Driver 4 "element '$Query' not found; nearest names: $($near -join ' | ')"
}

# Off-screen elements report infinite rectangles.
function ConvertTo-Px([double]$v) { if ([double]::IsFinite($v)) { [int]$v } else { 0 } }

function Get-Center($e) { $r = $e.Current.BoundingRectangle; [int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2) }

function Send-Mouse([int]$sx, [int]$sy, [string]$How) {
    Confirm-Foreground (Get-Target)
    [void][SqDrv]::SetCursorPos($sx, $sy); Start-Sleep -Milliseconds 80
    $down, $up = if ($How -eq 'RightClick') { 0x8, 0x10 } else { 0x2, 0x4 }
    $n = if ($How -eq 'DoubleClick') { 2 } else { 1 }
    foreach ($i in 1..$n) { [SqDrv]::mouse_event($down, 0, 0, 0, [IntPtr]::Zero); [SqDrv]::mouse_event($up, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60 }
}

# Client logical px (-X -Y) to screen px.
function Get-ClientPoint([int]$lx, [int]$ly) {
    $h = Get-Target; $scale = [SqDrv]::GetDpiForWindow($h) / 96.0
    $pt = New-Object SqDrv+POINT; [void][SqDrv]::ClientToScreen($h, [ref]$pt)
    [int]($pt.X + $lx * $scale), [int]($pt.Y + $ly * $scale)
}

function Get-ScreenPoint {
    if ($Name) {
        $e = Find-Element $Name $ControlType $Index
        if (-not $e) { Stop-Missing $Name }
        return Get-Center $e
    }
    if ($X -ge 0 -and $Y -ge 0) { return Get-ClientPoint $X $Y }
    Stop-Driver 6 'give -Name or -X and -Y'
}

$vk = @{ Ctrl = 0x11; Control = 0x11; Shift = 0x10; Alt = 0x12; Win = 0x5B; Enter = 0x0D; Esc = 0x1B; Escape = 0x1B; Tab = 0x09; Space = 0x20
    Up = 0x26; Down = 0x28; Left = 0x25; Right = 0x27; Home = 0x24; End = 0x23; PageUp = 0x21; PageDown = 0x22; Delete = 0x2E; Backspace = 0x08 }
function ConvertTo-VirtualKey([string]$k) {
    if ($vk.ContainsKey($k)) { return [uint16]$vk[$k] }
    if ($k -match '^[A-Za-z0-9]$') { return [uint16][char]$k.ToUpper() }
    if ($k -match '^F([1-9]|1[0-2])$') { return [uint16](0x6F + [int]$Matches[1]) }
    Stop-Driver 6 "unknown key '$k'"
}

switch ($Action) {
    'Dump' {
        function Write-Node($e, [int]$level) {
            $c = $e.Current; $r = $c.BoundingRectangle
            $line = "{0}{1} | Name='{2}' | Id='{3}' | Class='{4}' | [{5},{6},{7},{8}]" -f (' ' * (2 * $level)), ($c.ControlType.ProgrammaticName -replace '^ControlType\.', ''), $c.Name, $c.AutomationId, $c.ClassName, (ConvertTo-Px $r.X), (ConvertTo-Px $r.Y), (ConvertTo-Px $r.Width), (ConvertTo-Px $r.Height)
            if (-not $Filter -or $line.Contains($Filter, [StringComparison]::OrdinalIgnoreCase)) { Write-Output $line }
            if ($level -lt $Depth) {
                foreach ($ch in $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) { Write-Node $ch ($level + 1) }
            }
        }
        Write-Node (Get-Root) 0
        Write-Output 'ok: dump'
    }
    'Click' {
        if ($Mode -eq 'Invoke' -and $Name) {
            $e = Find-Element $Name $ControlType $Index
            if (-not $e) { Stop-Missing $Name }
            $pat = $null
            if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke(); Write-Output "ok: click '$Name' (Invoke)"; break }
            if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select(); Write-Output "ok: click '$Name' (SelectionItem)"; break }
            if ($e.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pat)) { $pat.Expand(); Write-Output "ok: click '$Name' (ExpandCollapse)"; break }
            if ($e.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle(); Write-Output "ok: click '$Name' (Toggle)"; break }
        }
        $sx, $sy = Get-ScreenPoint
        Send-Mouse $sx $sy $(if ($Mode -eq 'Invoke') { 'Mouse' } else { $Mode })
        Write-Output "ok: click $(if ($Name) { "'$Name'" } else { "$X,$Y" }) (Mouse $Mode)"
    }
    'Key' {
        if (-not $Keys) { Stop-Driver 6 '-Keys is required' }
        Confirm-Foreground (Get-Target)
        foreach ($chord in ($Keys -split '\s+' | Where-Object { $_ })) {
            [SqDrv]::Chord([uint16[]]@($chord -split '\+' | ForEach-Object { ConvertTo-VirtualKey $_ }))
            Start-Sleep -Milliseconds 120
        }
        Write-Output "ok: key $Keys"
    }
    'Type' {
        if (-not $Text) { Stop-Driver 6 '-Text is required' }
        Confirm-Foreground (Get-Target)
        [SqDrv]::TypeText($Text); Start-Sleep -Milliseconds 120
        Write-Output "ok: type $($Text.Length) chars"
    }
    'Wait' {
        if (-not $Name) { Stop-Driver 6 '-Name is required' }
        $end = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while ($true) {
            $found = [bool](Find-Element $Name $ControlType $Index)
            if ($found -ne [bool]$Gone) { Write-Output "ok: wait '$Name' $(if ($Gone) { 'gone' } else { 'present' })"; break }
            if ([DateTime]::UtcNow -gt $end) { Stop-Driver 5 "timeout waiting for '$Name' to $(if ($Gone) { 'disappear' } else { 'appear' })" }
            Start-Sleep -Milliseconds 250
        }
    }
    'Hover' {
        $sx, $sy = Get-ScreenPoint
        Confirm-Foreground (Get-Target)
        [void][SqDrv]::SetCursorPos($sx, $sy)
        Write-Output "ok: hover $sx,$sy"
    }
    'Focus-Dialog' {
        $p = Get-MainProcess
        $dialog = [SqDrv]::TopLevel([uint32]$p.Id) | Where-Object { $_ -ne $p.MainWindowHandle } | Select-Object -First 1
        if (-not $dialog) { if (Test-Path $targetFile) { Remove-Item $targetFile -Force }; Stop-Driver 1 "no secondary window of '$Process'" }
        Set-Content $targetFile ([long]$dialog)
        Confirm-Foreground $dialog
        Write-Output "ok: focus-dialog $([long]$dialog)"
    }
    'SelfTest' {
        # A throwaway WinForms window of our own (never a program that restores the user's documents).
        $title = "squill-selftest-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $stamp = "selftest é;'"
        $form = "Add-Type -AssemblyName System.Windows.Forms; `$f = New-Object Windows.Forms.Form; `$f.Text = '$title'; `$f.Width = 500; `$f.Height = 200; `$t = New-Object Windows.Forms.TextBox; `$t.Name = 'box'; `$t.Dock = 'Fill'; `$f.Controls.Add(`$t); `$f.Add_Shown({ `$f.Activate() }); [void]`$f.ShowDialog()"
        $started = Start-Process pwsh -ArgumentList '-NoProfile', '-Command', $form -PassThru
        try {
            $end = [DateTime]::UtcNow.AddSeconds(20)
            while ((Get-Process -Id $started.Id -ErrorAction SilentlyContinue).MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $end) { Start-Sleep -Milliseconds 300 }
            $h = (Get-Process -Id $started.Id).MainWindowHandle
            if ($h -eq 0) { Write-Output 'error: selftest: window did not open'; exit 6 }
            Set-Content $targetFile ([long]$h)
            $Process = 'pwsh'
            Confirm-Foreground $h
            Start-Sleep -Milliseconds 500
            [SqDrv]::TypeText($stamp); Start-Sleep -Milliseconds 400
            $edit = [System.Windows.Automation.AutomationElement]::FromHandle($h).FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
            $value = $edit.GetCurrentPropertyValue([System.Windows.Automation.ValuePattern]::ValueProperty)
            if ($value -ne $stamp) { Write-Output "error: selftest: edit control holds '$value'"; exit 6 }
            Write-Output 'selftest ok'
        } finally {
            if (-not $started.HasExited) { $started | Stop-Process -Force }
            if (Test-Path $targetFile) { Remove-Item $targetFile -Force }
        }
    }
}

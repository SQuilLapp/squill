#requires -Version 7
<# Resizes the main window of a process to a client area of Width x Height logical px and saves a PNG of it. Windows only. #>
param(
    [Parameter(Mandatory)][string]$Process,
    [Parameter(Mandatory)][string]$Out,
    [int]$Width = 1440,
    [int]$Height = 900,
    [ValidateSet('Screen', 'PrintWindow')][string]$Method = 'Screen')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SqWin {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
'@
[void][SqWin]::SetProcessDPIAware()
$p = Get-Process -Name $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Error "No window found for process '$Process'."; exit 1 }
$h = $p.MainWindowHandle
if ([SqWin]::IsIconic($h)) { Write-Error "The window of '$Process' is minimized."; exit 1 }
[void][SqWin]::ShowWindow($h, 1)
[void][SqWin]::SetForegroundWindow($h)
$scale = [SqWin]::GetDpiForWindow($h) / 96.0
$w = [int]($Width * $scale); $ht = [int]($Height * $scale)
$win = New-Object SqWin+RECT; $cli = New-Object SqWin+RECT
[void][SqWin]::GetWindowRect($h, [ref]$win); [void][SqWin]::GetClientRect($h, [ref]$cli)
$extraW = ($win.R - $win.L) - $cli.R; $extraH = ($win.B - $win.T) - $cli.B
$screen = $Method -eq 'Screen'
# The window is moved to the top-left corner. Screen: 0x0040 = SWP_SHOWWINDOW with HWND_TOPMOST (-1) so it is really on top
# (SetForegroundWindow alone is refused to a background process); PrintWindow: 0x0044 = SWP_NOZORDER | SWP_SHOWWINDOW.
$after = if ($screen) { [IntPtr](-1) } else { [IntPtr]::Zero }
$flags = if ($screen) { 0x0040 } else { 0x0044 }
[void][SqWin]::SetWindowPos($h, $after, 0, 0, $w + $extraW, $ht + $extraH, $flags)
try {
    Start-Sleep -Milliseconds 1200
    [void][SqWin]::GetWindowRect($h, [ref]$win); [void][SqWin]::GetClientRect($h, [ref]$cli)
    $pt = New-Object SqWin+POINT
    [void][SqWin]::ClientToScreen($h, [ref]$pt)
    if ($screen) {
        $c = New-Object SqWin+POINT; $c.X = $pt.X + [int]($cli.R / 2); $c.Y = $pt.Y + [int]($cli.B / 2)
        $top = [SqWin]::GetAncestor([SqWin]::WindowFromPoint($c), 2)
        $owner = [uint32]0; [void][SqWin]::GetWindowThreadProcessId($top, [ref]$owner)
        if ($owner -ne $p.Id) {
            $other = (Get-Process -Id $owner -ErrorAction SilentlyContinue).ProcessName
            [Console]::Error.WriteLine("Window of '$Process' is covered by '$other'")
            exit 2
        }
        $bmp = New-Object System.Drawing.Bitmap $cli.R, $cli.B
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($pt.X, $pt.Y, 0, 0, $bmp.Size)
        $g.Dispose()
        $how = "window on top: yes, method Screen"
    } else {
        $ww = $win.R - $win.L; $wh = $win.B - $win.T
        $full = New-Object System.Drawing.Bitmap $ww, $wh
        $g = [System.Drawing.Graphics]::FromImage($full)
        $hdc = $g.GetHdc()
        [void][SqWin]::PrintWindow($h, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
        $g.ReleaseHdc($hdc); $g.Dispose()
        $black = $true
        foreach ($i in 1..15) { foreach ($j in 1..15) {
            $px = $full.GetPixel([int]($ww * $i / 16), [int]($wh * $j / 16))
            if ($px.R -ne 0 -or $px.G -ne 0 -or $px.B -ne 0) { $black = $false }
        } }
        if ($black) {
            $full.Dispose()
            [Console]::Error.WriteLine("PrintWindow returned a black bitmap for '$Process' (DirectComposition window); use -Method Screen.")
            exit 3
        }
        $rect = New-Object System.Drawing.Rectangle ($pt.X - $win.L), ($pt.Y - $win.T), $cli.R, $cli.B
        $bmp = $full.Clone($rect, $full.PixelFormat)
        $full.Dispose()
        $how = "window on top: n/a, method PrintWindow"
    }
} finally {
    if ($screen) { [void][SqWin]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 0x0053) }   # HWND_NOTOPMOST, NOMOVE|NOSIZE|NOACTIVATE|SHOWWINDOW
}
$outFull = [IO.Path]::GetFullPath($Out)
$null = New-Item -ItemType Directory -Force (Split-Path -Parent $outFull)
$bmp.Save($outFull, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved $Out ($($bmp.Width)x$($bmp.Height) px, scale $scale, $how)"
$bmp.Dispose()

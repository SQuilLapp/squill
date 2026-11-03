#requires -Version 7
<# Resizes the main window of a process to a client area of Width x Height logical px and saves a PNG of it. Windows only. #>
param(
    [Parameter(Mandatory)][string]$Process,
    [Parameter(Mandatory)][string]$Out,
    [int]$Width = 1440,
    [int]$Height = 900)
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
# 0x0044 = SWP_NOZORDER | SWP_SHOWWINDOW; the window is moved to the top-left corner.
[void][SqWin]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, $w + $extraW, $ht + $extraH, 0x0044)
Start-Sleep -Milliseconds 1200
[void][SqWin]::GetClientRect($h, [ref]$cli)
$pt = New-Object SqWin+POINT
[void][SqWin]::ClientToScreen($h, [ref]$pt)
$bmp = New-Object System.Drawing.Bitmap $cli.R, $cli.B
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($pt.X, $pt.Y, 0, 0, $bmp.Size)
$g.Dispose()
$full = [IO.Path]::GetFullPath($Out)
$null = New-Item -ItemType Directory -Force (Split-Path -Parent $full)
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved $Out ($($bmp.Width)x$($bmp.Height) px, scale $scale)"
$bmp.Dispose()

# Launches Taveloom against a throwaway data dir (loopback only), captures its main window to a PNG, then closes it.
# Usage: capture-window.ps1 -Out shot.png [-Page Dashboard|Devices|Studio|Settings] [-Exe path]
param(
    [string]$Out = "shot.png",
    [string]$Exe = "$PSScriptRoot\..\apps\windows\Taveloom.App\bin\Debug\net10.0-windows\Taveloom.exe",
    [int]$Width = 1240, [int]$Height = 800,
    [string]$DataDir = (Join-Path $env:TEMP "taveloom-shot-data"),
    [int]$Wait = 5,
    [string]$Page = "Dashboard"
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class W32 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
[W32]::SetProcessDPIAware() | Out-Null
if (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe") { $env:DOTNET_ROOT = "$env:ProgramFiles\dotnet" }  # a user-level .NET install can shadow the machine-wide one
$env:TAVELOOM_PAGE = $Page; $env:TAVELOOM_DATA_DIR = $DataDir; $env:TAVELOOM_LAN = "0"
$p = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds $Wait
$p.Refresh()
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { Write-Host "No main window."; Stop-Process $p -Force; exit 1 }
$r = New-Object W32+RECT; [W32]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.R - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc(); [W32]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Captured ${w}x${ht} -> $Out"
Stop-Process $p -Force

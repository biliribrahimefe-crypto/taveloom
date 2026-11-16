# Runs the real Android client code (Dart) against a real Taveloom Windows host on loopback. No phone involved.
# Requires: .NET 10 SDK, Flutter on PATH. -AndroidDir must be a path with ASCII characters only (Dart tooling limitation).
# Usage: scripts/e2e-android-vs-host.ps1 -AndroidDir C:\dev\tav-android
# -ApproveDelaySeconds simulates a human taking time to click Allow (default 25 s: longer than two Android ping intervals).
param([Parameter(Mandatory)][string]$AndroidDir, [int]$ApproveDelaySeconds = 25)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if ($AndroidDir -match '[^\x00-\x7F]') { throw "AndroidDir must be ASCII-only." }
$flags = Join-Path $env:TEMP ("taveloom-e2e-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $flags | Out-Null
$out = Join-Path $flags "host.log"

dotnet build "$root\tests\tools\Taveloom.E2eHost" -c Release | Out-Null
$exe = Get-ChildItem "$root\tests\tools\Taveloom.E2eHost\bin\Release" -Recurse -Filter "Taveloom.E2eHost.exe" | Select-Object -First 1
$host_ = Start-Process -FilePath $exe.FullName -ArgumentList "`"$flags`" $($ApproveDelaySeconds * 1000)" -RedirectStandardOutput $out -PassThru -WindowStyle Hidden
try {
    $uri = $null
    for ($i = 0; $i -lt 60 -and -not $uri; $i++) {
        Start-Sleep -Milliseconds 500
        if (Test-Path $out) {
            $m = Select-String -Path $out -Pattern '^PAIR_URI=(.+)$' | Select-Object -First 1
            if ($m) { $uri = $m.Matches[0].Groups[1].Value }
        }
    }
    if (-not $uri) { throw "Host did not print a pairing URI." }
    $env:TAVELOOM_E2E_URI = $uri.Trim(); $env:TAVELOOM_E2E_FLAGDIR = $flags; $env:TAVELOOM_E2E_HOSTLOG = $out
    Push-Location $AndroidDir
    flutter test test/e2e_host_test.dart
    $code = $LASTEXITCODE
    Pop-Location
    "--- host log ---"; Get-Content $out
    exit $code
}
finally {
    New-Item -ItemType File (Join-Path $flags "quit.flag") -Force | Out-Null
    Start-Sleep -Seconds 1
    if (-not $host_.HasExited) { Stop-Process $host_ -Force }
}

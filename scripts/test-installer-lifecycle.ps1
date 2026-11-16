# Tests install -> upgrade -> uninstall (keep data) -> reinstall -> uninstall (delete data) with two TEST installers that were built with
#   /DMyAppId=<test guid> /DDataDirName=TaveloomInstallerTest /DFastTest
# so a real Taveloom installation and its real user data (%LOCALAPPDATA%\Taveloom) are never touched.
param(
    [Parameter(Mandatory)][string]$OldInstaller,
    [Parameter(Mandatory)][string]$NewInstaller,
    [string]$AppIdGuid = "AAAAAAAA-1111-2222-3333-BBBBBBBBBBBB",
    [string]$DataDirName = "TaveloomInstallerTest"
)
$ErrorActionPreference = "Stop"
$dir = Join-Path $env:TEMP "taveloom-install-test"
$data = Join-Path $env:LOCALAPPDATA $DataDirName
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$AppIdGuid}_is1"
$fail = 0
function Check($name, $ok) { if ($ok) { "PASS  $name" } else { "FAIL  $name"; $script:fail++ } }
function Install($exe) { Start-Process $exe -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=`"$dir`"" -Wait }
function Uninstall($extra) { Start-Process (Join-Path $dir "unins000.exe") -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", $extra -Wait }

if (Test-Path $dir) { throw "$dir exists; remove it first." }
New-Item -ItemType Directory -Force $data | Out-Null
"user data survives" | Set-Content (Join-Path $data "marker.txt")

Install $OldInstaller
Check "install: Taveloom.exe present" (Test-Path (Join-Path $dir "Taveloom.exe"))
Check "install: uninstall entry shows old version" ((Get-ItemProperty $key).DisplayVersion -eq "0.0.1-test")
Check "install: per-user, no admin (HKCU entry)" (Test-Path $key)

Install $NewInstaller
Check "upgrade: version replaced in place" ((Get-ItemProperty $key).DisplayVersion -eq "0.0.2-test")
Check "upgrade: user data untouched" ((Get-Content (Join-Path $data "marker.txt")) -eq "user data survives")
Check "upgrade: still one installation" (@(Get-ChildItem (Join-Path $dir "unins*.exe")).Count -eq 1)

Uninstall ""
Check "uninstall: program files removed" (-not (Test-Path (Join-Path $dir "Taveloom.exe")))
Check "uninstall: registry entry removed" (-not (Test-Path $key))
Check "uninstall (default): user data KEPT" (Test-Path (Join-Path $data "marker.txt"))

Install $NewInstaller
Uninstall "/DELETEDATA=1"
Check "uninstall /DELETEDATA=1: user data removed" (-not (Test-Path $data))
Check "uninstall: install dir gone" (-not (Test-Path (Join-Path $dir "Taveloom.exe")))

if (Test-Path $dir) { Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue }
if ($fail) { "$fail check(s) FAILED"; exit 1 } else { "All lifecycle checks passed." }

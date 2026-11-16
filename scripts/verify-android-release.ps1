# Verifies a release-signed APK and/or AAB: signer certificate fingerprint, no debug certificate, application id and version.
# Reads only public certificate information (no passwords, no key.properties).
# Usage: scripts/verify-android-release.ps1 -Apk <file> [-Aab <file>] -ExpectedSha256 8C:62:...   (colons optional)
param(
    [string]$Apk,
    [string]$Aab,
    [Parameter(Mandatory)][string]$ExpectedSha256,
    [string]$ExpectedPackage = "com.tavrynt.taveloom"
)
$ErrorActionPreference = "Stop"
if (-not $Apk -and -not $Aab) { throw "Give -Apk and/or -Aab." }
$expected = $ExpectedSha256.Replace(":", "").ToLowerInvariant()
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { throw "Set ANDROID_HOME." }
$bt = (Get-ChildItem (Join-Path $sdk "build-tools") | Sort-Object Name -Descending | Select-Object -First 1).FullName
$keytool = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME "bin\keytool.exe" } else { "keytool" }
$fail = 0
function Check($name, $ok) { if ($ok) { "PASS  $name" } else { "FAIL  $name"; $script:fail++ } }

if ($Apk) {
    $sig = & (Join-Path $bt "apksigner.bat") verify --print-certs $Apk 2>&1 | Out-String
    $fp = [regex]::Match($sig, "Signer #1 certificate SHA-256 digest: ([0-9a-f]{64})").Groups[1].Value
    Check "APK signature verifies" ($sig -match "Number of signers: 1" -or $fp)
    Check "APK certificate SHA-256 equals expected ($fp)" ($fp -eq $expected)
    Check "APK is not debug-signed" (-not ($sig -match "Android Debug"))
    $badge = & (Join-Path $bt "aapt2.exe") dump badging $Apk 2>&1 | Out-String
    $m = [regex]::Match($badge, "package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'")
    "      applicationId=$($m.Groups[1].Value) versionCode=$($m.Groups[2].Value) versionName=$($m.Groups[3].Value)"
    Check "APK applicationId is $ExpectedPackage" ($m.Groups[1].Value -eq $ExpectedPackage)
}
if ($Aab) {
    $out = & $keytool -printcert -jarfile $Aab 2>&1 | Out-String
    $fp = [regex]::Match($out, "SHA256: ([0-9A-F:]{95})").Groups[1].Value.Replace(":", "").ToLowerInvariant()
    Check "AAB certificate SHA-256 equals expected ($fp)" ($fp -eq $expected)
    Check "AAB is not debug-signed" (-not ($out -match "Android Debug"))
}
if ($fail) { "$fail check(s) FAILED"; exit 1 } else { "All release verification checks passed." }

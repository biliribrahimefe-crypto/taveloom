# Tests the release-signing gate in apps/android/android/app/build.gradle.kts against a REAL Flutter/Gradle build.
# Run from an ASCII-only copy of apps/android (see docs/development/building.md). Needs Flutter, JDK 17-21 and the Android SDK on the path.
#
# Failure cases use throw-away fake/test values created in a temp folder; your real signing files are never read by this script
# except, when -ExpectedSha256 is given, through Gradle's normal consumption of <home>\.taveloom\signing\key.properties in the last case.
param(
    [Parameter(Mandatory)][string]$AndroidDir,
    [string]$ExpectedSha256,
    [switch]$SkipFailureCases,  # only run the final real-key build + verification (needs -ExpectedSha256)
    [string]$ResultFile        # optional: PASS/FAIL lines are also appended here so a long run can be watched
)
# Native tools (keytool, apksigner, flutter) write progress to stderr; under "Stop" PowerShell would treat that as a fatal error.
$ErrorActionPreference = "Continue"
if ($AndroidDir -match '[^\x00-\x7F]') { throw "AndroidDir must be ASCII-only." }
$apk = Join-Path $AndroidDir "build\app\outputs\flutter-apk\app-release.apk"
$tmp = Join-Path $env:TEMP ("tav-signing-test-" + [guid]::NewGuid().ToString("N")); New-Item -ItemType Directory $tmp | Out-Null
$fail = 0
function Check($name, $ok) {
    $line = if ($ok) { "PASS  $name" } else { $script:fail++; "FAIL  $name" }
    $line
    if ($ResultFile) { Add-Content -Path $ResultFile -Value $line }
}

# Builds with the given environment overrides; returns exit code and the log text. Never prints the log.
function Build($envs, $target = "apk") {
    $stale = if ($target -eq "appbundle") { Join-Path $AndroidDir "build\app\outputs\bundle\release\app-release.aab" } else { $apk }
    if (Test-Path $stale) { Remove-Item $stale -Force }
    foreach ($k in $envs.Keys) { Set-Item "Env:$k" $envs[$k] }
    $log = Join-Path $tmp "build.log"
    Push-Location $AndroidDir
    # cmd.exe handles the redirection; calling flutter with PowerShell redirection can hang when the console is hidden.
    try { & cmd.exe /c "flutter build $target --release > `"$log`" 2>&1"; $code = $LASTEXITCODE } finally { Pop-Location; foreach ($k in $envs.Keys) { Remove-Item "Env:$k" -ErrorAction SilentlyContinue } }
    [pscustomobject]@{ Code = $code; Log = (Get-Content $log -Raw); Apk = (Test-Path $apk) }
}

if (-not $SkipFailureCases) {
# --- 1. signing file missing
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = (Join-Path $tmp "does-not-exist.properties") }
Check "missing signing file: build fails" ($r.Code -ne 0)
Check "missing signing file: clear message, no APK produced" ($r.Log -match "Release signing is not configured" -and -not $r.Apk)

# --- 2. file present but incomplete (fake values)
$bad = Join-Path $tmp "incomplete.properties"; "storePassword=fake-test-value`nkeyAlias=x" | Set-Content $bad
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = $bad }
Check "incomplete signing file: build fails, names missing keys" ($r.Code -ne 0 -and $r.Log -match "missing required keys" -and -not $r.Apk)

# --- 3. keystore path does not exist
$nok = Join-Path $tmp "nokeystore.properties"
"storeFile=$($tmp -replace '\\','/')/missing.jks`nstorePassword=fake-test-value`nkeyAlias=x`nkeyPassword=fake-test-value" | Set-Content $nok
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = $nok }
Check "keystore file missing: build fails" ($r.Code -ne 0 -and $r.Log -match "keystore named by storeFile does not exist" -and -not $r.Apk)

# --- 4. debug opt-in WITHOUT CI is ignored (local builds can never be debug-signed)
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = (Join-Path $tmp "does-not-exist.properties"); TAVELOOM_ALLOW_DEBUG_SIGNING = "1"; CI = "" }
Check "opt-in without CI=true: still fails" ($r.Code -ne 0 -and -not $r.Apk)

# --- 5. wrong password on a throw-away keystore: fails, never falls back, never prints the password
$jks = Join-Path $tmp "throwaway.jks"; $good = [guid]::NewGuid().ToString("N"); $wrong = [guid]::NewGuid().ToString("N")
$keytool = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME "bin\keytool.exe" } else { "keytool" }
$env:TAV_TEST_PW = $good
& $keytool -genkeypair -keystore $jks -alias test -keyalg RSA -keysize 2048 -validity 2 -storepass:env TAV_TEST_PW -keypass:env TAV_TEST_PW -dname "CN=Throwaway Test" 2>&1 | Out-Null
Remove-Item Env:\TAV_TEST_PW
$wp = Join-Path $tmp "wrongpw.properties"
"storeFile=$($jks -replace '\\','/')`nstorePassword=$wrong`nkeyAlias=test`nkeyPassword=$wrong" | Set-Content $wp
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = $wp }
Check "wrong password: build fails, no APK (no debug fallback)" ($r.Code -ne 0 -and -not $r.Apk)
Check "wrong password: password never appears in the build output" (-not $r.Log.Contains($wrong) -and -not $r.Log.Contains($good))

# --- 6. explicit CI-only debug opt-in works (what GitHub Actions uses)
$r = Build @{ TAVELOOM_SIGNING_PROPERTIES = (Join-Path $tmp "does-not-exist.properties"); TAVELOOM_ALLOW_DEBUG_SIGNING = "1"; CI = "true" }
Check "CI=true + TAVELOOM_ALLOW_DEBUG_SIGNING=1: debug-signed test APK is produced" ($r.Code -eq 0 -and $r.Apk)
if ($r.Apk -and $env:ANDROID_HOME) {
    $bt = (Get-ChildItem (Join-Path $env:ANDROID_HOME "build-tools") | Sort-Object Name -Descending | Select-Object -First 1).FullName
    $sig = & (Join-Path $bt "apksigner.bat") verify --print-certs $apk 2>&1 | Out-String
    Check "CI opt-in APK really is debug-signed (explicit, visible)" ($sig -match "Android Debug")
}

}

# --- 7. valid configuration at the default location signs with the release certificate
if ($ExpectedSha256) {
    $env:TAVELOOM_SIGNING_PROPERTIES = ""; $env:TAVELOOM_ALLOW_DEBUG_SIGNING = ""
    $r = Build @{} "apk"
    $aabR = Build @{} "appbundle"
    Check "default config: APK and AAB build" ($r.Code -eq 0 -and $aabR.Code -eq 0)
    $aab = Join-Path $AndroidDir "build\app\outputs\bundle\release\app-release.aab"
    & (Join-Path $PSScriptRoot "verify-android-release.ps1") -Apk $apk -Aab $aab -ExpectedSha256 $ExpectedSha256
    Check "release certificate verification" ($LASTEXITCODE -eq 0)
}

Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
if ($fail) { "$fail check(s) FAILED"; exit 1 } else { "All signing-gate checks passed." }

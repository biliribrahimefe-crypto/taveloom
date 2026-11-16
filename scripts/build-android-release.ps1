# Builds the release-signed Android APK and AAB from the CURRENT git commit with developer-neutral build paths.
#
# Why a drive letter: Dart/Flutter embed absolute source paths (the Flutter SDK, the pub cache, the project) in libapp.so. Building from
# C:\Users\<you>\... would put your user name into every APK. This script maps a spare drive letter to a work folder with ASCII-only
# paths (so Flutter's analysis server also works) and builds from there, so the paths in the binary are e.g. B:/tav-android/lib/main.dart.
#
# Expected layout of -WorkRoot:  <WorkRoot>\flutter   (Flutter SDK)   <WorkRoot>\pub-cache   <WorkRoot>\<any>   (project mirror is created)
# Signing: Gradle reads <home>\.taveloom\signing\key.properties itself; this script never reads or prints it. Release builds fail without it.
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][string]$ExpectedCertSha256,
    [string]$Drive = "B",
    [string]$ExpectedPackage = "com.tavrynt.taveloom"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
function Native($exe, [string[]]$arguments) { & $exe @arguments; if ($LASTEXITCODE) { throw "$exe failed with exit code $LASTEXITCODE" } }

$status = git -C $root status --porcelain
if ($status) { throw "Working tree is not clean; commit first so the binaries correspond to a commit." }
$commit = (git -C $root rev-parse HEAD).Trim()

if (-not $env:ANDROID_HOME -or -not $env:JAVA_HOME) { throw "Set ANDROID_HOME and JAVA_HOME (JDK 17-21)." }
if (Test-Path "${Drive}:\") { throw "Drive ${Drive}: is already in use; pass another -Drive." }
subst "${Drive}:" $WorkRoot
try {
    $mirror = "${Drive}:\tav-android"
    $env:PUB_CACHE = "${Drive}:\pub-cache"
    $env:Path = "${Drive}:\flutter\bin;$($env:JAVA_HOME)\bin;$($env:Path)"
    Remove-Item Env:\TAVELOOM_R8 -ErrorAction SilentlyContinue            # R8 stays off for this beta
    Remove-Item Env:\TAVELOOM_ALLOW_DEBUG_SIGNING -ErrorAction SilentlyContinue
    Remove-Item Env:\TAVELOOM_SIGNING_PROPERTIES -ErrorAction SilentlyContinue

    robocopy "$root\apps\android" $mirror /MIR /XD build .dart_tool .gradle /XF local.properties key.properties /NFL /NDL /NJH /NJS /NP | Out-Null
    Push-Location $mirror
    try {
        Native flutter @("pub", "get")
        if (Test-Path "$mirror\build\app\outputs") { Remove-Item "$mirror\build\app\outputs" -Recurse -Force }
        Native flutter @("build", "apk", "--release")
        Native flutter @("build", "appbundle", "--release")
    }
    finally { Pop-Location }

    $apk = "$mirror\build\app\outputs\flutter-apk\app-release.apk"
    $aab = "$mirror\build\app\outputs\bundle\release\app-release.aab"
    & "$PSScriptRoot\verify-android-release.ps1" -Apk $apk -Aab $aab -ExpectedSha256 $ExpectedCertSha256 -ExpectedPackage $ExpectedPackage
    if ($LASTEXITCODE) { throw "Release verification failed; not publishing artifacts." }

    $out = Join-Path $root "artifacts\android-signed"
    New-Item -ItemType Directory -Force $out | Out-Null
    $pubspecVersion = (Select-String -Path "$root\apps\android\pubspec.yaml" -Pattern '^version:\s*(\S+)').Matches[0].Groups[1].Value
    $name = "Taveloom-" + (([xml](Get-Content "$root\Directory.Build.props")).Project.PropertyGroup.Version) + "-android"
    Copy-Item $apk "$out\$name.apk" -Force
    Copy-Item $aab "$out\$name.aab" -Force
    $info = [ordered]@{
        kind = "android"; commit = $commit; dirty = $false; builtAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        pubspecVersion = $pubspecVersion; r8 = $false; certSha256 = $ExpectedCertSha256
        files = [ordered]@{
            "$name.apk" = (Get-FileHash "$out\$name.apk" -Algorithm SHA256).Hash.ToLower()
            "$name.aab" = (Get-FileHash "$out\$name.aab" -Algorithm SHA256).Hash.ToLower()
        }
    }
    $info | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root "artifacts\build-info-android.json") -Encoding utf8
    "Built from commit $commit"; "  $out\$name.apk"; "  $out\$name.aab"
}
finally { subst "${Drive}:" /d | Out-Null }

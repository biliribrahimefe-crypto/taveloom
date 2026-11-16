# Assembles the closed-beta tester package from the verified build outputs, checks it, checksums it and archives it.
# Output: artifacts\closed-beta\v<Version>\  and  artifacts\closed-beta\Taveloom-<Version>-closed-beta.zip
# Refuses to continue if: the git tree is dirty, a binary does not map to the current commit, the APK is not signed with the expected release
# certificate, or any forbidden/sensitive content is found. Nothing is uploaded or sent anywhere.
param(
    [string]$Version,                         # default: from Directory.Build.props
    [string]$AndroidVersion = "0.2.0-beta (build 2)",
    [Parameter(Mandatory)][string]$ExpectedCertSha256,
    [string]$ExpectedAndroidVersionName = "0.2.0-beta",
    [int]$ExpectedAndroidVersionCode = 2
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Fail($m) { throw "PACKAGE REFUSED: $m" }

if (-not $Version) { $Version = ([xml](Get-Content "$root\Directory.Build.props")).Project.PropertyGroup.Version }
if (git status --porcelain) { Fail "git working tree is not clean." }
$commit = (git rev-parse HEAD).Trim()

# --- inputs and their provenance
$art = "$root\artifacts"
$setup = "$art\Taveloom-Setup-$Version-win-x64.exe"; $zip = "$art\Taveloom-$Version-win-x64.zip"; $apk = "$art\android-signed\Taveloom-$Version-android.apk"
foreach ($f in $setup, $zip, $apk) { if (-not (Test-Path $f)) { Fail "missing $f" } }
foreach ($kind in "windows", "android") {
    $bi = Get-Content "$art\build-info-$kind.json" -Raw | ConvertFrom-Json
    # The binaries may be older than HEAD only if nothing that goes into them changed since.
    $changed = git diff --name-only $bi.commit HEAD -- apps packages installer Directory.Build.props scripts/build-installer.ps1 scripts/build-android-release.ps1
    if ($LASTEXITCODE -or $changed) { Fail "build-info-$kind.json says commit $($bi.commit); build inputs changed since ($changed). Rebuild." }
    $buildCommit = $bi.commit
    foreach ($p in $bi.files.PSObject.Properties) {
        $path = Get-ChildItem $art -Recurse -File -Filter $p.Name | Select-Object -First 1
        if (-not $path -or (Get-FileHash $path.FullName -Algorithm SHA256).Hash.ToLower() -ne $p.Value) { Fail "$($p.Name) does not match build-info-$kind.json (modified after the build?)." }
    }
}

# --- Android: release certificate, package, version, no debug signing
& "$PSScriptRoot\verify-android-release.ps1" -Apk $apk -ExpectedSha256 $ExpectedCertSha256
if ($LASTEXITCODE) { Fail "APK verification failed." }
$sdkBt = (Get-ChildItem (Join-Path $env:ANDROID_HOME "build-tools") | Sort-Object Name -Descending | Select-Object -First 1).FullName
$badge = & "$sdkBt\aapt2.exe" dump badging $apk 2>&1 | Out-String
$m = [regex]::Match($badge, "versionCode='(\d+)' versionName='([^']+)'")
if ([int]$m.Groups[1].Value -ne $ExpectedAndroidVersionCode -or $m.Groups[2].Value -ne $ExpectedAndroidVersionName) { Fail "APK version is $($m.Groups[2].Value) ($($m.Groups[1].Value)), expected $ExpectedAndroidVersionName ($ExpectedAndroidVersionCode)." }

# --- scan the binaries for personal data / secrets
$needles = @($env:USERNAME, "OneDrive", "key.properties", "storePassword", "taveloom-release", "github_pat_", "ghp_", "gho_") | Where-Object { $_ }
# Docs may legitimately name key.properties; they must not contain the user name, cloud paths or secrets.
$docNeedles = @($env:USERNAME, "OneDrive", "storePassword=", "github_pat_", "ghp_", "gho_") | Where-Object { $_ }
function ScanZip($path, $badExtensions) {
    $found = @()
    $z = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        foreach ($e in $z.Entries) {
            if ($e.FullName.EndsWith("/")) { continue }
            if ($badExtensions | Where-Object { $e.FullName.ToLower().EndsWith($_) }) { $found += "forbidden file: $($e.FullName)"; continue }
            $ms = New-Object IO.MemoryStream; $s = $e.Open(); $s.CopyTo($ms); $s.Dispose()
            $text = [Text.Encoding]::Latin1.GetString($ms.ToArray())
            $wide = [Text.Encoding]::Latin1.GetString([Text.Encoding]::Unicode.GetBytes($text.Substring(0, 0)))   # (UTF-16 handled below per needle)
            foreach ($n in $needles) {
                $u16 = [Text.Encoding]::Latin1.GetString([Text.Encoding]::Unicode.GetBytes($n))
                if ($text.IndexOf($n, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $text.Contains($u16)) { $found += "'$n' in $($e.FullName)" }
            }
        }
    }
    finally { $z.Dispose() }
    $found
}
$bad = @(ScanZip $zip @(".pdb", ".db", ".jks", ".keystore", ".pfx", ".pem", ".p12", ".log")) + @(ScanZip $apk @(".jks", ".keystore", ".pfx", ".pem", ".p12", ".db", ".log"))
if ($bad) { $bad | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }; Fail "personal data or sensitive content found in the binaries (see above)." }
# the installer payload is the same publish folder as the zip (already scanned); also record embedded product version
$tmp = Join-Path $env:TEMP ("tav-pkg-" + [guid]::NewGuid().ToString("N")); New-Item -ItemType Directory $tmp | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($zip, "$tmp\win")
$pv = (Get-Item "$tmp\win\Taveloom.exe").VersionInfo.ProductVersion
"Taveloom.exe product version: $pv"

# --- assemble the folder
$outRoot = "$art\closed-beta"; $dir = "$outRoot\v$Version"
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item $setup, $zip, $apk $dir
$cert = $ExpectedCertSha256.ToUpperInvariant()
$map = @{ "{{VERSION}}" = $Version; "{{ANDROID_VERSION}}" = $AndroidVersion; "{{SOURCE_COMMIT}}" = $buildCommit; "{{CERT_SHA256}}" = $cert }
foreach ($doc in Get-ChildItem "$root\docs\closed-beta" -Filter *.md) {
    $t = Get-Content $doc.FullName -Raw
    foreach ($k in $map.Keys) { $t = $t.Replace($k, $map[$k]) }
    if ($t -match "\{\{[A-Z_]+\}\}") { Fail "unresolved placeholder in $($doc.Name)" }
    Set-Content -Path "$dir\$($doc.Name)" -Value $t -Encoding utf8
}
foreach ($f in "PRIVACY.md", "THIRD_PARTY_NOTICES.md", "LICENSE", "NOTICE.md", "SECURITY.md", "SUPPORT.md") { Copy-Item "$root\$f" $dir }

# --- forbidden content check on the assembled folder
$forbiddenNames = "*.aab", "*.jks", "*.keystore", "*.pfx", "key.properties", "*.db", "*.log", "*.p12", "*.pem"
$hit = Get-ChildItem $dir -Recurse -File -Include $forbiddenNames
if ($hit) { Fail "forbidden file in package: $($hit.Name -join ', ')" }
$sig = & "$sdkBt\apksigner.bat" verify --print-certs "$dir\Taveloom-$Version-android.apk" 2>&1 | Out-String
if ($sig -match "Android Debug") { Fail "debug-signed APK in package." }
foreach ($f in Get-ChildItem $dir -File) {
    if ($f.Length -eq 0) { Fail "empty file $($f.Name)" }
    if ($f.Attributes -band ([IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System)) { Fail "hidden/system file $($f.Name)" }
    if ($f.Extension -in ".md", ".txt") { $c = Get-Content $f.FullName -Raw; foreach ($n in $docNeedles) { if ($c.IndexOf($n, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Fail "'$n' found in $($f.Name)" } } }
}

# --- checksums (sha256sum format: hash, two spaces, name) and archive
$sums = Get-ChildItem $dir -File | Where-Object Name -ne "SHA256SUMS.txt" | Sort-Object Name | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
Set-Content "$dir\SHA256SUMS.txt" -Value $sums -Encoding ascii
$archive = "$outRoot\Taveloom-$Version-closed-beta.zip"
if (Test-Path $archive) { Remove-Item $archive }
$staging = "$tmp\Taveloom-$Version-closed-beta"; Copy-Item $dir $staging -Recurse
Compress-Archive -Path $staging -DestinationPath $archive -CompressionLevel Optimal

# --- verify the archive end to end against the checksums
$ver = "$tmp\verify"; [IO.Compression.ZipFile]::ExtractToDirectory($archive, $ver)
$inner = Get-ChildItem $ver -Directory | Select-Object -First 1
$ok = $true
foreach ($line in Get-Content "$($inner.FullName)\SHA256SUMS.txt") {
    $h, $n = $line -split "  ", 2
    $actual = (Get-FileHash "$($inner.FullName)\$n" -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $h) { "MISMATCH $n"; $ok = $false }
}
$listed = (Get-Content "$($inner.FullName)\SHA256SUMS.txt" | ForEach-Object { ($_ -split "  ", 2)[1] }) | Sort-Object
$present = Get-ChildItem $inner.FullName -File | Where-Object Name -ne "SHA256SUMS.txt" | ForEach-Object Name | Sort-Object
if (Compare-Object $listed $present) { "file list differs from SHA256SUMS.txt"; $ok = $false }
Remove-Item $tmp -Recurse -Force
if (-not $ok) { Fail "archive verification failed." }

"Package OK (binaries built from commit $buildCommit; packaged at HEAD $commit)"
"Folder : $dir"
"Archive: $archive  ($([math]::Round((Get-Item $archive).Length / 1MB, 1)) MB, SHA256 $((Get-FileHash $archive -Algorithm SHA256).Hash.ToLower()))"
Get-ChildItem $dir -File | ForEach-Object { "  {0,-48} {1,8:N2} MB" -f $_.Name, ($_.Length / 1MB) }

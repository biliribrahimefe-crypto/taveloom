# Builds the self-contained Windows app, a portable zip and a per-user installer (Inno Setup 6 required: winget install JRSoftware.InnoSetup).
# Output (all under artifacts\): Taveloom-Setup-<version>-win-x64.exe, Taveloom-<version>-win-x64.zip, build-info-windows.json.
# The binaries are UNSIGNED unless you sign them yourself. Requires a clean git working tree so every binary maps to a commit.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$props = [xml](Get-Content "$root\Directory.Build.props")
$version = $props.Project.PropertyGroup.Version
$numeric = ($version -replace '-.*$', '') + ".0"
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found." }
if (git -C $root status --porcelain) { throw "Working tree is not clean; commit first so the binaries correspond to a commit." }
$commit = (git -C $root rev-parse HEAD).Trim()

$publish = "$root\artifacts\windows-x64"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
if (Test-Path "$root\artifacts\installer") { Remove-Item "$root\artifacts\installer" -Recurse -Force }
# Distribution builds carry no debug symbols and no developer build paths or user name: DebugType=none removes the PDBs and the PDB path
# inside the assemblies; ContinuousIntegrationBuild turns on deterministic path normalisation.
dotnet publish "$root\apps\windows\Taveloom.App" -c Release -r win-x64 --self-contained true -o $publish `
    -p:DebugType=none -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE) { throw "publish failed" }
if (Get-ChildItem $publish -Recurse -Filter *.pdb) { throw "Debug symbols (.pdb) found in the publish output." }

& $iscc "/DMyAppVersion=$version" "/DMyAppNumericVersion=$numeric" "$root\installer\taveloom.iss"
if ($LASTEXITCODE) { throw "installer build failed" }

$setup = Get-ChildItem "$root\artifacts\installer" -Filter "*.exe" | Select-Object -First 1
Copy-Item $setup.FullName "$root\artifacts" -Force
$zip = "$root\artifacts\Taveloom-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$publish\*" -DestinationPath $zip -CompressionLevel Optimal

$info = [ordered]@{
    kind = "windows"; commit = $commit; dirty = $false; builtAtUtc = (Get-Date).ToUniversalTime().ToString("o"); version = $version
    files = [ordered]@{}
}
$info.files[$setup.Name] = (Get-FileHash "$root\artifacts\$($setup.Name)" -Algorithm SHA256).Hash.ToLower()
$info.files[(Split-Path $zip -Leaf)] = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$info | ConvertTo-Json -Depth 4 | Set-Content "$root\artifacts\build-info-windows.json" -Encoding utf8
"Built from commit $commit"
$info.files.GetEnumerator() | ForEach-Object { "{0}  SHA256 {1}" -f $_.Key, $_.Value }

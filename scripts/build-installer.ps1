# Builds the self-contained Windows app and wraps it in a per-user installer (Inno Setup 6 required: winget install JRSoftware.InnoSetup).
# Output: artifacts\installer\Taveloom-Setup-<version>-win-x64.exe (unsigned unless you sign it yourself).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$props = [xml](Get-Content "$root\Directory.Build.props")
$version = $props.Project.PropertyGroup.Version
$numeric = ($version -replace '-.*$', '') + ".0"
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found." }

$publish = "$root\artifacts\windows-x64"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish "$root\apps\windows\Taveloom.App" -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE) { throw "publish failed" }
& $iscc "/DMyAppVersion=$version" "/DMyAppNumericVersion=$numeric" "$root\installer\taveloom.iss"
if ($LASTEXITCODE) { throw "installer build failed" }
Get-ChildItem "$root\artifacts\installer" -Filter "*.exe" | ForEach-Object { "{0}  {1:N1} MB  SHA256 {2}" -f $_.Name, ($_.Length / 1MB), (Get-FileHash $_.FullName -Algorithm SHA256).Hash }

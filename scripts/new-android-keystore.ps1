# Creates the Android RELEASE signing key and the signing configuration that Gradle reads from OUTSIDE the repository:
#   <your home>\.taveloom\signing\key.properties
# Run this ONCE, on a trusted machine. You will be prompted for passwords; nothing is printed or committed.
#
#   The keystore is the identity of the app. If you lose it or its password, existing installs can no longer be updated.
#   BACK IT UP (password manager + offline copy) and NEVER commit it. See docs/release/android-signing.md.
param(
    [string]$KeystorePath = (Join-Path $HOME "taveloom-release.jks"),
    [string]$Alias = "taveloom",
    [string]$SigningDir = (Join-Path $HOME ".taveloom\signing")
)
$ErrorActionPreference = "Stop"
$props = Join-Path $SigningDir "key.properties"
if (Test-Path $KeystorePath) { throw "$KeystorePath already exists. Refusing to overwrite a signing key." }
if (Test-Path $props) { throw "$props already exists. Refusing to overwrite a signing configuration." }
New-Item -ItemType Directory -Force $SigningDir | Out-Null
$keytool = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME "bin\keytool.exe" } else { "keytool" }
$pw = Read-Host "Keystore/key password (min 8 chars)" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw))
if ($plain.Length -lt 8) { throw "Password too short." }
# The password goes to keytool through an environment variable so it never appears on a command line / in the process list.
$env:TAVELOOM_KS_PW = $plain
try {
    & $keytool -genkeypair -v -keystore $KeystorePath -alias $Alias -keyalg RSA -keysize 4096 -validity 10000 `
        -storepass:env TAVELOOM_KS_PW -keypass:env TAVELOOM_KS_PW -dname "CN=Taveloom Release, OU=Tavrynt Studios"
    if ($LASTEXITCODE) { throw "keytool failed" }
}
finally { Remove-Item Env:\TAVELOOM_KS_PW -ErrorAction SilentlyContinue }
@"
storeFile=$($KeystorePath -replace '\\','/')
storePassword=$plain
keyAlias=$Alias
keyPassword=$plain
"@ | Set-Content $props -Encoding ascii
$plain = $null
# Gradle needs the passwords in key.properties; limit the file (and the keystore) to the current user.
foreach ($f in @($props, $KeystorePath)) { icacls $f /inheritance:r /grant:r "$($env:USERNAME):(R,W)" | Out-Null }
"Created $KeystorePath and $props (outside the repository). Back up the keystore NOW."

# Creates the Android RELEASE signing key and the git-ignored android/key.properties that Gradle reads.
# Run this ONCE, on a trusted machine. You will be prompted for passwords; nothing is printed or committed.
#
#   The keystore is the identity of the app. If you lose it or its password, existing installs can no longer be updated.
#   BACK IT UP (password manager + offline copy) and NEVER commit it. See docs/release/android-signing.md.
param(
    [string]$KeystorePath = (Join-Path $HOME "taveloom-release.jks"),
    [string]$Alias = "taveloom",
    [string]$AndroidDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "apps\android\android")
)
$ErrorActionPreference = "Stop"
if (Test-Path $KeystorePath) { throw "$KeystorePath already exists. Refusing to overwrite a signing key." }
$keytool = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME "bin\keytool.exe" } else { "keytool" }
$pw = Read-Host "Keystore/key password (min 8 chars)" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw))
if ($plain.Length -lt 8) { throw "Password too short." }
& $keytool -genkeypair -v -keystore $KeystorePath -alias $Alias -keyalg RSA -keysize 4096 -validity 10000 `
    -storepass $plain -keypass $plain -dname "CN=Taveloom, O=EFEDEV"
if ($LASTEXITCODE) { throw "keytool failed" }
@"
storeFile=$($KeystorePath -replace '\\','/')
storePassword=$plain
keyAlias=$Alias
keyPassword=$plain
"@ | Set-Content (Join-Path $AndroidDir "key.properties") -Encoding ascii
"Created $KeystorePath and $AndroidDir\key.properties (git-ignored). Back up the keystore NOW."

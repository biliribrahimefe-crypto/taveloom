# Android signing and versioning

## Versioning

`apps/android/pubspec.yaml`: `version: <versionName>+<versionCode>`; currently `0.2.0-beta+2`.
- `versionName` is shown to users. `versionCode` must **increase with every build you give to anyone** (Android refuses downgrades).
- Windows uses `Directory.Build.props` (`<Version>0.2.0-beta.1</Version>`). Keep the two release numbers aligned.

## Where the signing configuration lives

Release signing is configured **outside the repository**, in your user profile:

| File | Path | Contents |
| --- | --- | --- |
| Keystore | `%USERPROFILE%\taveloom-release.jks` (any path you choose) | the private key (never leaves your machine) |
| Signing config | `%USERPROFILE%\.taveloom\signing\key.properties` | `storeFile`, `storePassword`, `keyAlias`, `keyPassword` |

`build.gradle.kts` finds the config under the current user's home directory (`user.home`/`.taveloom/signing/key.properties`); no user name is
hard-coded. Neither file is in, or may ever be put in, the repository or a folder that syncs to a cloud service (OneDrive, Dropbox, ...).
Build scripts and Gradle never print or log the values.

## What happens when signing is missing or wrong

A **release** build (`flutter build apk|appbundle --release`, i.e. Gradle `assemble/bundle/packageRelease`) is **rejected** when
- the signing file is missing, or lacks any of the four keys, or
- the keystore it names does not exist, or
- the keystore/passwords/alias are wrong (the signing step itself fails).

There is **no fallback to the debug key**; no unsigned or debug-signed release APK/AAB is produced locally. Debug builds
(`flutter run`, `flutter build apk --debug`) are unaffected and use the debug key as usual.

### CI-only exception

GitHub Actions has no release key. Its workflow sets `TAVELOOM_ALLOW_DEBUG_SIGNING=1` for the APK step; the Gradle script honours it
**only when `CI=true`** (set by GitHub Actions), so it cannot take effect on a developer machine by accident. Those APKs are test builds,
signed with a throw-away debug key. Never distribute them.

`TAVELOOM_SIGNING_PROPERTIES` can point to another signing file; it exists for the automated tests of this logic
(`scripts/test-android-signing-gate.ps1`).

## Creating the release key (once)

On a trusted machine:
```powershell
$env:JAVA_HOME = "<path to JDK 17-21>"
.\scripts\new-android-keystore.ps1
```
It prompts for a password (hidden), passes it to `keytool` through an environment variable (not the command line), creates the keystore, writes
`key.properties` under `.taveloom\signing`, and restricts both files to your user. It refuses to overwrite an existing key or config.

Rules:
1. **Back up the `.jks` and its password** (password manager plus an offline copy; verify the backup opens with `keytool -list`).
2. **Never commit, upload, paste or sync** the keystore, `key.properties` or passwords. CI never receives them.
3. If a key may have leaked: stop distributing, create a new key, and tell users they must reinstall (Android will not update an app
   signed with a different key). With Play App Signing, request an upload-key reset instead (see
   [google-play-and-signing.md](google-play-and-signing.md)).

## Building and verifying a release

```powershell
cd apps/android              # use an ASCII-only copy of this folder, see docs/development/building.md
flutter build apk --release
flutter build appbundle --release
.\scripts\verify-android-release.ps1 -Apk build\app\outputs\flutter-apk\app-release.apk `
    -Aab build\app\outputs\bundle\release\app-release.aab -ExpectedSha256 <published certificate SHA-256>
```
The verifier prints only public data: the certificate SHA-256, whether it is a debug certificate, and the application id/version.
Current release certificate (SHA-256):
`8C:62:05:9B:88:B9:DB:36:99:E4:39:14:C2:43:C7:E8:67:97:AD:63:A1:50:73:35:48:42:33:F3:71:95:A8:40`
(`CN=Taveloom Release, OU=Tavrynt Studios`). Publish it in the release notes so users can check downloads.

`scripts/test-android-signing-gate.ps1 -AndroidDir <ascii copy> [-ExpectedSha256 ...]` runs real builds for every failure mode
(missing file, incomplete file, missing keystore, debug opt-in without CI, wrong password) plus the CI opt-in and, optionally, a real signed build.

## Google Play

Not published and out of scope for this beta. See [google-play-and-signing.md](google-play-and-signing.md) for Play App Signing versus
direct APKs and update compatibility.

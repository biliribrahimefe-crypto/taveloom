# Android signing and versioning

## Versioning

`apps/android/pubspec.yaml`: `version: <versionName>+<versionCode>`; currently `0.2.0-beta+2`.
- `versionName` is shown to users. `versionCode` must **increase with every build you give to anyone** (Android refuses downgrades).
- Windows uses `Directory.Build.props` (`<Version>0.2.0-beta.1</Version>`). Keep the two release numbers aligned.

## What is signed with what today

Without `android/key.properties`, `flutter build apk|appbundle --release` signs with the **debug key** of the machine that built it.
That is fine for local testing and CI artifacts, but **not for distribution**:
- the debug key differs per machine, so a build made elsewhere cannot update an installed one;
- Google Play rejects debug-signed bundles.

## Creating the release key (once)

On a trusted machine run `scripts/new-android-keystore.ps1`. It asks for a password, creates `taveloom-release.jks` in your home
folder, and writes the git-ignored `apps/android/android/key.properties` that `build.gradle.kts` reads.

Rules:
1. **Back up the `.jks` and its password** (password manager plus an offline copy). Losing either means installed apps can never be updated.
2. **Never commit** the keystore or `key.properties` (both are in `.gitignore`; verify with `git status` before every commit).
3. Do not paste the passwords into issues, CI logs or chat.
4. If a key may have leaked: stop distributing, create a new key, and tell users they must reinstall (Android will not accept an update
   signed with a different key). If you use Play App Signing, request an upload-key reset in the Play Console instead.

## Building releases

```powershell
cd apps/android            # use an ASCII-only path, see docs/development/building.md
flutter build apk --release        # sideload / GitHub release
flutter build appbundle --release  # Google Play (upload key), requires the release key
```

Verify the signer before publishing:
```powershell
<android-sdk>\build-tools\<ver>\apksigner.bat verify --print-certs build\app\outputs\flutter-apk\app-release.apk
```
The certificate DN must be `CN=Taveloom Release, OU=Tavrynt Studios`, **not** `Android Debug`.
Record the certificate SHA-256 in the release notes so testers can check it.

## CI

CI builds the APK without a key (debug-signed) and uploads it as a 14-day workflow artifact for testing only.
To sign in CI later, store the keystore as an encrypted secret (base64) and write `key.properties` in a workflow step; do not
do this for a public repository without restricting the workflow to protected tags/branches.

## Google Play

Not published and out of scope for this beta. Before submitting: create the release key, enrol in Play App Signing, add a privacy
policy URL (use `PRIVACY.md`), and complete the Data safety form (the app collects no data; camera is used for QR scanning).

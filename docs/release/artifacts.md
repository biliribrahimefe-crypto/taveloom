# Release artifacts: which is which

Taveloom produces two kinds of Android/Windows builds. Do not mix them up.

| Kind | Produced by | Android signing | Intended use |
| --- | --- | --- | --- |
| **Distribution artifacts** | The maintainer's machine, `scripts/package-closed-beta.ps1` | **Release key** (`CN=Taveloom Release, OU=Tavrynt Studios`, SHA-256 `8C:62:05:9B:88:B9:DB:36:99:E4:39:14:C2:43:C7:E8:67:97:AD:63:A1:50:73:35:48:42:33:F3:71:95:A8:40`) | Given to invited testers (closed beta). Windows binaries are unsigned. |
| **CI test artifacts** | GitHub Actions (`taveloom-android-apk`, 14 days) | **Debug key** (explicit CI-only opt-in, no release key exists in CI) | Proving that the project builds. **Never distribute.** They cannot update, or be updated by, a release-signed install. |

The Android App Bundle (`.aab`) is kept locally for future Google Play work and is **not** part of the tester package.

## Traceability
Every distribution package records the source commit and the SHA-256 of each file (`SHA256SUMS.txt`). Windows binaries embed the commit in their
product version (`Taveloom.exe` → Properties → Details). The Android certificate fingerprint is checked by `scripts/verify-android-release.ps1`.

## What must never be in a distribution package
Keystores, `key.properties`, passwords, tokens, debug-signed APKs, the AAB, databases or logs, pairing QR codes, and developer build paths/usernames
(`scripts/package-closed-beta.ps1` checks for these and refuses to build the package if it finds any).

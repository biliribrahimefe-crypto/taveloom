# Taveloom 0.2.0-beta (controlled beta)

> **Beta software.** Not production-ready, not independently security-audited, unsigned builds. Use it on networks and PCs you control.

Taveloom turns an Android phone into a control surface for a Windows 11 PC, with workspaces that adapt to the app you are using.
Full description: [README](../../README.md).

## Downloads

| File | For | Notes |
| --- | --- | --- |
| `Taveloom-Setup-0.2.0-beta.1-win-x64.exe` | Windows 11 | Per-user installer (no admin). **Unsigned**: SmartScreen warns. |
| `Taveloom-0.2.0-beta.1-win-x64.zip` | Windows 11 | Portable, self-contained. Unsigned. |
| `Taveloom-0.2.0-beta.1-android.apk` | Android 7.0+ | Sideload. Debug-signed until a release key exists. |
| `Taveloom-0.2.0-beta.1-android.aab` | (maintainers) | Not for end users; cannot be uploaded to Google Play while debug-signed. |
| `SHA256SUMS.txt` | all | Verify every download before running it. |

Install and pairing steps: [Windows](../installation/windows.md), [Android](../installation/android.md).

## What's new since 0.1.0

**Fixes found on a physical phone and in code review**
- Pairing no longer fails when you take 20+ seconds to click **Allow** on the PC. The host kept waiting without answering the
  phone's connection checks, so Android hung up. (Confirmed on a phone with 25 s and 40 s delays.)
- "Reconnect now" no longer leaves two live sessions on the PC.
- The QR scanner works on the tested phone (the release build no longer uses R8 shrinking; see [r8-shrinking](r8-shrinking.md)).
- The pairing QR no longer lists virtual-adapter addresses; the phone tries all listed addresses at once (faster, fewer timeouts).
- The app reconnects promptly when it returns from the background.
- Rejected pairing and similar final messages are now reliably delivered before the connection closes.

**New**
- Windows installer (per-user, upgrade-in-place, keeps your data on uninstall unless you choose otherwise).
- Android: backups disabled, cleartext traffic disabled, app name "Taveloom", versioned `0.2.0-beta (2)`, optional release signing.
- The audit log keeps only the newest 2000 events.
- Privacy statement, support guide and third-party notices.

## What was tested

- Automated: 117 .NET unit/integration tests; Flutter analyze/format/unit tests; real Android client code against the real Windows host;
  installer install/upgrade/uninstall lifecycle (11 checks); GitHub Actions on the pull request.
- Manual, on **one** physical Android phone with the Windows app: QR scanning, immediate and delayed pairing approval, pairing
  rejection and timeout, device revocation, Play/Pause, volume slider, screenshot, Wi-Fi interruption and reconnection, PC app
  restart and reconnection, Android background/resume.

## Known limitations

- Tested on one phone and Windows 11 only; Windows 10 is unsupported. Other devices, routers and Wi-Fi setups may behave differently.
- Windows installer/executables are **unsigned**; the Android APK is debug-signed (updating from a build signed with another key requires uninstalling and re-pairing).
- Not tested: upgrading the installer while Taveloom is running, tablets, non-English Windows, Android versions other than the tested phone's.
- Media keys are sent to whatever player Windows chooses; Windows does not report whether it reacted. No track info.
- Hotkeys cannot reach elevated (administrator) windows.
- Workspace rules are evaluated when focus changes, not when only a window title changes.
- No automatic discovery: if the PC's address changes, scan a new QR code. Port/LAN changes need a restart of Taveloom.
- The first Windows Firewall prompt must be answered; choose private networks.
- R8 shrinking is off, so the APK is ~3.6 MB larger.
- No independent security review. See the [security model](../security/security-model.md) for residual risks.

## Privacy

No accounts, no telemetry, no external servers. See [PRIVACY.md](../../PRIVACY.md).

## Upgrading

Windows: run the new installer; settings and paired phones are kept. Android: install the new APK over the old one if both were
signed with the same key; otherwise uninstall first and pair again.

## Reporting problems

Use the issue templates; read [SUPPORT.md](../../SUPPORT.md). Security issues: [SECURITY.md](../../SECURITY.md).

## License

MIT. See [LICENSE](../../LICENSE) and [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).

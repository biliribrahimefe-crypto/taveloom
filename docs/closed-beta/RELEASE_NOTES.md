# Taveloom {{VERSION}}: release notes (closed beta)

Private build for invited testers. Not a public release. Not independently security-audited.

| | |
| --- | --- |
| Windows app | {{VERSION}} (installer and portable zip), Windows 11 x64 |
| Android app | {{ANDROID_VERSION}}, `com.tavrynt.taveloom`, Android 7.0+ |
| Source commit | `{{SOURCE_COMMIT}}` |
| Android signing certificate (SHA-256) | `{{CERT_SHA256}}` |
| Windows signing | **none** (unsigned: SmartScreen will warn) |

## What Taveloom does
- Pair an Android phone with a Windows 11 PC over your local network using a QR code, a pinned TLS certificate and an explicit **Allow** on the PC.
- Controls on the phone: buttons, toggles, live value tiles (CPU, memory, volume, mic state), media controls and a volume slider. Actions: hotkeys,
  launch app/folder/URL, media keys, volume/mute, screenshot, lock PC. The phone sends only control ids; the PC decides what runs.
- **Adaptive workspaces:** the PC detects the focused application and shows the matching workspace on the phone (rules, priorities, default workspace,
  manual override). Starter workspaces: Development, Productivity, Media (fully editable in Workspace Studio).
- Device management: approve, disconnect, revoke and rename phones; audit log kept locally (newest 2000 events).

## Changes since 0.1.0
- Fixed: pairing failed if you took ~20 s or longer to click Allow (the PC stopped answering the phone's connection checks).
- Fixed: duplicate sessions after "Reconnect now"; QR codes no longer list virtual adapters; the phone tries all addresses at once; quicker reconnect after returning from the background.
- Fixed: the QR scanner on the tested phone (this build does not use code shrinking, see known issues).
- Added: Windows installer, Android release signing, privacy statement, device-name validation, audit-log pruning, Android backups/cleartext traffic disabled.
- **Android application ID changed** to `com.tavrynt.taveloom` (earlier test builds used `dev.efedev.taveloom`); the new app installs beside the old one and must be paired again.
- Distribution builds now omit debug symbols and developer build paths.

## What was tested
**Automated:** 117 .NET unit/integration tests (real TLS WebSocket host), Flutter format/analyze/unit tests, the real Android client code against the real
Windows host, an install/upgrade/uninstall lifecycle test of the installer, and checks of the Android signing configuration (including the failure cases).
GitHub Actions ran the .NET and Flutter jobs on the source commit.

**Manually, by the developer, on one Windows 11 PC and one physical Android phone:** release-signed APK installation, QR pairing, immediate/delayed
approval, denial and timeout, Play/Pause, volume slider, screenshot, Wi-Fi disconnect/reconnect, Android background/resume, Windows restart/reconnect, device
revocation, Windows installer install/reinstall and pairing persistence across reinstall.

**Not done:** an independent security review, testing on other phones/Android versions/networks/Windows setups, Windows 10, tablets, upgrade-while-running,
Google Play distribution.

## Known issues
See `KNOWN_ISSUES.md`.

## Privacy
No accounts, no telemetry, no external servers. Data stays on your PC (`%LOCALAPPDATA%\Taveloom`) and your phone. See `PRIVACY.md`.

## Licenses
MIT (see `LICENSE`; original copyright preserved, see `NOTICE.md`). Dependency licenses: `THIRD_PARTY_NOTICES.md`.

## Feedback
See `FEEDBACK_TEMPLATE.md` and `TESTER_CHECKLIST.md`.

# Changelog

All notable changes are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

Release candidate for **0.2.0-beta** (versions set to `0.2.0-beta.1` / Android `0.2.0-beta+2`). Not tagged or released yet.
See [docs/release/RELEASE_NOTES_0.2.0-beta.md](docs/release/RELEASE_NOTES_0.2.0-beta.md).

### Changed
- **Android application ID and namespace: `dev.efedev.taveloom` -> `com.tavrynt.taveloom`** (a different app: installs side by side,
  re-pair required; see docs/release/package-migration.md). Windows installer `AppId` and data paths are unchanged.
- Publisher shown in the Windows app/installer is now Tavrynt Studios (a brand, not a legal entity); the EFEDEV copyright and MIT license
  are preserved. Added `NOTICE.md`.
- Release-signing key DN; Google Play App Signing vs direct APK guidance (docs/release/google-play-and-signing.md).
- The keystore script passes the password to `keytool` through an environment variable (not the command line) and restricts
  `key.properties` to the current user.

### Security
- Android release signing now reads `<home>/.taveloom/signing/key.properties` (outside the repository and cloud-synced folders). Release builds
  **fail** when signing is missing or invalid and never fall back to debug signing; GitHub Actions uses an explicit CI-only opt-in
  (`TAVELOOM_ALLOW_DEBUG_SIGNING=1` together with `CI=true`) for test APKs. New `scripts/test-android-signing-gate.ps1` and
  `scripts/verify-android-release.ps1`.

### Release engineering
- Windows per-user installer (Inno Setup) with upgrade-in-place and optional data removal; install/upgrade/uninstall lifecycle test script.
- Android: backups and cleartext traffic disabled, app label fixed, optional release signing, `scripts/new-android-keystore.ps1`,
  AAB build, R8 switch (`TAVELOOM_R8=1`, default off) with documented evidence and procedure.
- CI pins `ubuntu-24.04`, Flutter 3.47.9, JDK 21.
- Audit log is pruned to the newest 2000 events at start.
- Docs: PRIVACY, SUPPORT, THIRD_PARTY_NOTICES, installation guides, release notes and checklist.

### Fixed
- **Pairing failed when the user waited ~20 s before clicking Allow** (found on a physical phone: "The connection closed before
  pairing finished"). Root cause: the host awaited the human approval *on the socket receive loop*, so it stopped reading the
  connection and never answered protocol-level WebSocket pings; the Android client (`pingInterval` 10 s) dropped the unresponsive
  peer after the second missed pong. The approval now runs as a background task while the receive loop keeps answering pings;
  while approval is pending only `ping` is accepted. Regression tests: `HostAnswersPingsWhileWaitingForHumanApproval`,
  `OnlyPingIsAcceptedWhileApprovalIsPending`, plus the real Dart client against the real host with a 25 s approval
  (reproduced the failure before the fix). The approval time limit (60 s) and the explicit Allow requirement are unchanged.
- Host: a drained send queue no longer cuts the receive loop short, so the peer can read the final frames (e.g. "declined").
- Windows host: pairing approval was cancelled by the 15 s unauthenticated-connection timeout, so a user who clicked Allow after
  15 seconds saw the pairing rejected. The timeout now stops once a human approval is pending (the 60 s approval limit still applies).
- Android client: restarting the connection ("Reconnect now") left the previous session alive beside the new one (two live sessions
  on the host) and the old session could later clear the new one's socket. Superseded sessions are now closed and ignored.
- Android client: pairing-code addresses are dialled in parallel instead of one by one (each unreachable address used to cost
  4-6 s), and socket writes on a closing connection no longer throw.
- Android client: returning to the foreground now skips the reconnect backoff or verifies the existing connection is alive.
- Windows host: the pairing QR no longer lists virtual-adapter addresses (Hyper-V, WSL, ...) when a routed interface exists.
- Android: `dart format` was failing on `test/protocol_test.dart`, which would have failed CI.
- Android QR scanner: the generic "camera not available" message now shows the real error code; release builds no longer
  run R8 shrinking (suspected to break the ML Kit/CameraX scanner stack; not yet confirmed on a device).

### Added
- Regression tests: slow human approval, re-pairing after revocation, QR address selection.
- `tests/tools/Taveloom.E2eHost` and `scripts/e2e-android-vs-host.ps1`: run the real Android (Dart) client against a real host on
  loopback (pairing, pinning, replay refusal, sync, actions, restart regression, revocation). Not a device test.
- Android release signing via a git-ignored `android/key.properties`; CI pins Flutter 3.47.9 and JDK 21 and builds the APK.

### Added
- Windows host: pinned-TLS WebSocket server, QR pairing with explicit approval, challenge-response authentication, device
  revocation, rate limiting, audit log.
- Action engine: hotkeys, app/folder/URL launchers, media keys, system volume and mute, screenshot, lock workstation.
- Workspaces with validation, persistence, three editable templates, and the adaptive engine (event-driven foreground detection,
  priority rules, default fallback, manual override, debounce).
- Live widgets: button, toggle, value display, media control, volume slider.
- WPF app: dashboard, device management, Workspace Studio (drag and drop), settings, tray, dark/light theme.
- Flutter Android client: analyzes clean, unit tests pass, release APK builds; not yet run on a device.

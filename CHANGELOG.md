# Changelog

All notable changes are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed
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

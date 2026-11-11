# Security model

Taveloom lets a phone trigger actions on your PC, so the design starts from "assume the local network is hostile".

**Status:** this software has not been independently audited. Do not describe it as "secure" or "audited" to others.

## What is protected, and how

| Threat | Mitigation | Where |
| --- | --- | --- |
| Eavesdropping / tampering on Wi-Fi | TLS 1.2/1.3 only | `HostServer` |
| Impersonating the PC | Client pins the certificate fingerprint received via QR; no CA trust | `HostClient` (Dart), `HostCertificate` |
| Unknown phone connects | One-time token (2 min) **and** explicit Allow on Windows; no approver means rejection | `PairingService` |
| Stolen / replayed credential traffic | Credential sent once at pairing; later logins use HMAC challenge-response with a fresh nonce | `AuthProof` |
| Credential theft at rest | Windows: DPAPI (current user). Android: Keystore-backed EncryptedSharedPreferences | `DeviceRegistry`, `SecureStore` |
| Lost phone | Revoke on Windows: immediate disconnect, credential unusable | `DeviceRegistry.Revoke` |
| Brute force | 5 failures / minute / address, then `rate_limited`; 128-bit+ random tokens and credentials | `AuthThrottle` |
| Remote command execution | None exists. The phone sends a control id; the host resolves it in the active workspace. No shell, no scripts, no arbitrary paths. | `ClientSession`, `ActionEngine` |
| Dangerous launch targets | Apps must be absolute `.exe`/`.lnk` paths; URLs must be `http(s)`; all configured locally | `LaunchHandler` |
| Public exposure | Requests from non-private addresses get HTTP 403; LAN exposure can be switched off | `NetworkGuard` |
| Flooding | 64 KiB message cap, 20-burst/10-per-second action limit, 128-request replay cache, bounded send queue | `ClientSession` |
| Duplicate execution | A request id runs at most once | `ClientSession` |
| Secrets in logs | Audit log records events and device ids only; tests assert credentials never appear | `AuditLog` |

## Residual risks (read these)

* **Approved phone = control of everything in your workspaces.** Anyone holding an approved, unlocked phone can press your buttons,
  including "Lock PC" or any app you configured. Use the phone's screen lock and revoke lost devices.
* **The pairing QR is a secret for 2 minutes.** Someone who photographs it and reaches your LAN first can *request* pairing, but
  still needs your approval on the PC. Read the device name in the approval dialog.
* **Same-user malware.** DPAPI protects against other users and offline copies, not against malware running as you.
* **Hotkeys and launchers act with your privileges.** A configured hotkey can do anything that key can do. Taveloom cannot send
  input to elevated (administrator) windows (UIPI) and reports that as a failure.
* **Local network only.** Taveloom does not authenticate the network itself. There is no cloud relay, by design.
* **No signature on builds.** Unsigned executables trigger SmartScreen; verify what you run.
* **Foreground detection** reads only the process name and window title, in memory, to match your rules. Nothing is stored or sent.

## Privacy

No accounts, no telemetry, no external servers. Data stays in `%LOCALAPPDATA%\Taveloom` and on the phone.

## Reporting vulnerabilities

See `SECURITY.md`.

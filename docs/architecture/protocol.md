# Taveloom protocol (v1)

Transport: **WSS** (`wss://<host>:<port>/ws`), text frames, one JSON object per message, max 64 KiB. Binary frames are rejected.
Reference implementations: `packages/protocol/Taveloom.Protocol` (C#) and `apps/android/lib/protocol.dart` (Dart). Both are
checked against the same HMAC test vector.

## Envelope

```json
{ "v": 1, "type": "action.invoke", "id": "k3j2h1g0f9d8", "ts": 1760000000000, "payload": { } }
```

`v` must equal `1`. `id` correlates requests with replies. Unknown message types get an `error` reply; five malformed messages
close the connection. Property names are camelCase; enums are camelCase strings.

## TLS and trust

The Windows host generates a self-signed ECDSA P-256 certificate on first run (10 year validity, private key protected with DPAPI).
Clients **never** use the system trust store for this connection. They pin the certificate's SHA-256 fingerprint, delivered
out-of-band in the pairing QR code, and refuse any other certificate.

## Pairing (once per device)

QR payload: `taveloom://pair?v=1&h=<ip,ip>&p=<port>&f=<sha256-hex>&t=<token>&n=<host name>`

1. Windows creates a single-use token valid for 2 minutes and shows the QR.
2. Phone connects (pinned), sends `pair.request {token, deviceId, deviceName}`.
3. Host validates and consumes the token, replies `pair.pending`, and asks the Windows user to **Allow / Deny**.
4. On Allow: host stores a fresh 32-byte credential (DPAPI-protected) and replies `pair.approved {deviceId, credential, hostName}`.
   The credential is sent exactly once, over the pinned TLS channel.
5. The phone stores the credential in the Android Keystore (EncryptedSharedPreferences).

Failures: `auth_failed` (unknown token), `pairing_expired`, `pairing_rejected`, `rate_limited`.

## Authentication (every connection)

Challenge-response, so the credential is not re-sent:

```
phone -> auth.hello {deviceId}
host  -> auth.challenge {nonce}
phone -> auth.response {proof}      proof = base64url(HMAC-SHA256(credential, "taveloom-auth-v1|" + nonce + "|" + deviceId))
host  -> auth.ok {hostName, protocolVersion}, then workspace.state, telemetry
```

Test vector: credential = bytes `00..1F`, nonce `nonce-abc`, deviceId `dev-12345678` →
`EKj-sPXs0muqUyu7ZrbbJgMoc1WosmlfHWYJT_3cy6A`.

Unauthenticated connections are closed after 15 s. Five failed attempts from one address block it for one minute.
Revoked devices receive `device_revoked`; connected ones get `device.revoked` and are disconnected immediately.

## State synchronisation

`workspace.state {revision, mode, reason, active, workspaces}` is authoritative and carries a monotonically increasing `revision`
(per host process). Clients apply a state only if its revision is newer than the one they hold, except the first state after a
(re)connect, then reply `state.ack {revision}`. `mode` is `auto` or `manual`; `reason` explains why the workspace is active
(`Rule: Code.exe`, `Default workspace`, `Manual selection`).

Each control in `active.controls` has `available` / `unavailableReason`. The phone never sees executable paths, URLs, or hotkeys,
only what is needed to draw the tile.

Client → host: `workspace.select {workspaceId}`, `workspace.auto {enabled}`.

`telemetry {cpuPercent, ramPercent, volumePercent, muted, micMuted}` is pushed every 2 s **only while a phone is connected**.
A missing field means "not available", never zero.

## Actions

```
phone -> action.invoke   id=<requestId> {controlId, value?}
host  -> action.result   id=<requestId> {requestId, controlId, ok, timestamp, error?, message?, state?}
```

* The phone sends a **control id**, never a command. The host looks the id up in the *currently active* workspace; anything else
  returns `unknown_control`.
* A request id is executed at most once. Repeating it replays the stored result.
* `value` is only used by volume sliders (0-100, validated).
* Limits: 20 burst / 10 per second per connection (`rate_limited`), 10 s execution timeout (`timeout`).
* `ok: true` means the host performed the operation it names. For media keys that is "key sent to Windows"; Windows does not
  report whether a player acted on it, and the result message says so.

## Error object

`{code, message, requestId?}` with codes: `invalid_message`, `unsupported_version`, `unauthenticated`, `auth_failed`,
`pairing_expired`, `pairing_rejected`, `device_revoked`, `rate_limited`, `unknown_control`, `action_unavailable`, `action_failed`,
`timeout`, `unknown_workspace`, `internal`.

## Icons

Workspace files use portable icon names (`code`, `terminal`, `folder`, `globe`, `camera`, `play_pause`, `skip_next`, `skip_previous`,
`volume`, `mute`, `lock`, `file`, `calculator`, `note`, `cpu`, `memory`, `mic`, `star`, `briefcase`, `music`, `download`, `link`,
`settings`, `keyboard`, `home`). Windows maps them to Segoe Fluent Icons, Android to Material icons.

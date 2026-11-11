# Architecture overview

```
 Android (Flutter)                         Windows 11 (.NET 10)
┌──────────────────────┐   WSS, pinned    ┌─────────────────────────────────────────────┐
│ UI: onboarding,      │◄────────────────►│ Taveloom.Host (library)                     │
│ workspaces, tiles    │  JSON envelopes  │  Server   Kestrel + WebSocket sessions      │
│ AppState             │                  │  Security cert, pairing, device registry    │
│ HostClient (reconnect│                  │  Actions  hotkeys, launch, media, system    │
│ heartbeat, pinning)  │                  │  Workspaces  model, rules, adaptive engine  │
│ Keystore credentials │                  │  Storage  SQLite + DPAPI                    │
└──────────────────────┘                  ├─────────────────────────────────────────────┤
                                          │ Taveloom.App (WPF, MVVM) uses the Host      │
                                          └─────────────────────────────────────────────┘
```

The Windows side is the **authority**: it owns devices, workspaces, rules and every action definition. The phone is a thin client
that draws what it is told and reports taps.

## Projects

| Path | Purpose |
| --- | --- |
| `packages/protocol/Taveloom.Protocol` | Wire contract (envelope, messages, QR payload, auth proof). No Windows dependencies. |
| `apps/windows/Taveloom.Host` | Everything except UI: host server, security, actions, workspaces, adaptive engine, storage. `TaveloomHost` is the composition root. |
| `apps/windows/Taveloom.App` | WPF shell (dashboard, devices, Workspace Studio, settings, tray). |
| `apps/android` | Flutter client. |
| `tests/windows/Taveloom.Host.Tests` | Unit and integration tests; integration tests run a real host over real TLS WebSockets. |

## Key decisions

* **Host as a library.** The UI and the tests drive the same `TaveloomHost`, so what is tested is what ships.
* **Control ids, not commands.** The phone cannot name a program, path, URL or key; it can only press a control that exists in the
  active workspace on the PC.
* **Event-driven adaptivity.** Foreground changes arrive through `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, so there is no polling.
  Changes must be stable for 400 ms before a switch (debounce). A manual choice becomes an override that holds until the user
  resumes automatic switching.
* **Rule resolution.** Highest priority wins; at equal priority a rule that also matches the window title beats one that does not;
  remaining ties go to workspace order. No match falls back to the default workspace; if there is none the current one stays.
* **Honest results.** Unavailable features report `available: false` with a reason. Telemetry that cannot be read is `null`.
  A media key reports "sent", not "played".
* **Revisions.** Every client-visible change bumps a revision so stale state cannot overwrite newer state.
* **Persistence.** SQLite in `%LOCALAPPDATA%\Taveloom` (devices, workspaces, settings, audit). Device credentials and the TLS key are
  DPAPI-protected (current user).
* **Telemetry cost.** CPU, RAM and volume are sampled every 2 s only while a phone is connected, using `GetSystemTimes`,
  `GlobalMemoryStatusEx` and Core Audio.

## Not built yet

LAN discovery (UDP/mDNS; pairing uses the QR address list), media track info, a plugin runtime, an installer, auto-update.

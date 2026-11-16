# Privacy

Taveloom is local-first. There is no account, no cloud service, no analytics and no telemetry. The Windows app and the Android app
talk only to each other over your local network.

## What stays on your PC (`%LOCALAPPDATA%\Taveloom`)

| Data | Why | Protection |
| --- | --- | --- |
| Paired devices: id, name you gave, pairing time, last seen | Device management | SQLite file |
| Device credentials | Authenticating phones | Encrypted with Windows DPAPI (current user) |
| TLS certificate and key | Identity pinned by phones | Key protected with DPAPI |
| Workspaces, controls, rules, settings | Your configuration. Controls may contain file paths, URLs and hotkeys you entered | SQLite file, not encrypted |
| Audit log (newest 2000 events) | Security history: pairing, logins, action ids, results, phone IP addresses | SQLite file; never contains credentials or tokens |
| Screenshots you trigger | The "Screenshot" action saves to `Pictures\Taveloom` | Normal files; they contain whatever was on screen |

The app reads the **name and window title of the focused application** in memory to choose a workspace. It is not stored or sent.
While a phone is connected, CPU, memory and volume are sampled every 2 s and sent to that phone only.

## What stays on your phone

Device id, the PC's address and certificate fingerprint, and the pairing credential, stored in Android's Keystore-backed secure storage.
Android Auto Backup is disabled for the app, so this data is not copied to Google backups. The camera is used only to scan the
pairing QR code; no image is saved or sent anywhere.

## Network

Phones connect to the PC over TLS on your local network (default port 47821). Only private-network addresses are accepted.
Taveloom never contacts the developer or any other server. (Your browser may of course contact the sites behind URL buttons you configure.)

## Third parties

The Android app bundles Google's ML Kit barcode model (via `mobile_scanner`) and runs it on the device. We have not audited
whether ML Kit transmits diagnostics; see Google's ML Kit documentation and terms.

## Removing your data

- Revoke a phone: Taveloom → Devices → Revoke (its credential stops working immediately).
- Uninstall the Windows app and choose to delete settings when asked, or delete `%LOCALAPPDATA%\Taveloom`.
- On Android: Settings → Apps → Taveloom → Storage → Clear data, or "Forget this PC" in the app menu.

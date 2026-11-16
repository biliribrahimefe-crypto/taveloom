# Known issues and limitations ({{VERSION}})

Please read before reporting. Items marked **(untested)** have not been exercised, not even by the developer.

## Security and trust
- **No independent security audit** has been performed. The design (pinned TLS, one-time QR token plus explicit approval, challenge-response, revocation) is
  described in `PRIVACY.md` and the repository's `docs/security`, but treat Taveloom as beta software.
- An approved phone can press **every** control in your workspaces (including "Lock PC" and anything you configured). Keep the phone locked; revoke lost phones.
- Anyone who photographs the pairing QR code within its 2 minutes can *request* pairing but still needs your click on **Allow**. Read the device name in the prompt.
- The Windows installer and `Taveloom.exe` are **not code-signed** (SmartScreen warning). The Android APK is signed with the Taveloom release key, but that key
  has no external reputation. Always check the SHA-256 hashes.
- The PC accepts connections only from private-network addresses, but it does not authenticate the Wi-Fi itself. Do not use it on untrusted networks.

## Compatibility (very limited testing)
- Windows: **Windows 11 only**, tested on one PC. Windows 10 and other editions/languages **(untested)**.
- Android: tested on **one** physical phone. Other phones, manufacturers' battery savers, Android versions, tablets and foldables **(untested)**.
  No claim of multi-device compatibility is made.
- Routers with "AP isolation"/guest networks block the connection. VPNs that capture local traffic may too **(untested)**.

## Connection
- No automatic discovery: the phone learns the PC's address from the QR code. If the PC gets a new IP address, scan a new QR code.
- Changing the port or the "allow phones" setting needs a Taveloom restart and a new QR code.
- The first Windows Firewall prompt must be answered (private networks). If dismissed, allow Taveloom manually.
- Pairing approval times out after 60 seconds; the QR code after 2 minutes.

## Controls and workspaces
- Media keys (play/pause, next, previous) are sent to whatever player Windows chooses; Windows does not say whether it reacted. No track information is shown.
- Hotkeys and media keys cannot reach windows running **as administrator**.
- Workspace rules match the focused app (and window title) **when focus changes**. Changes of the title alone, such as switching browser tabs, do not re-trigger rules.
- Starter workspaces assume nothing is installed: controls such as "Project" or "VS Code" show *Not set up* until you give them a target in Workspace Studio.
- Windows may show the Taveloom window briefly when pairing prompts appear while another full-screen app is active **(untested)**.

## Installation and updates
- Upgrading the Windows installer **while Taveloom is running** and installing over a portable copy **(untested)**.
- No automatic updates. Install new builds manually.
- If an Android update fails with a signature error, uninstall the old app and pair again.
- The Android APK is larger than necessary (about 68 MB) because code shrinking is switched off in this beta.
- Not on Google Play.

## Not implemented
LAN auto-discovery, track/now-playing info, tablets-specific layouts, Windows 10 support, a Google Play release, auto-update, internet/remote access.

## Where to report
See `FEEDBACK_TEMPLATE.md`. Security issues: `SECURITY.md` (do not post them publicly).

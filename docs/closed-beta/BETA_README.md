# Taveloom {{VERSION}}: closed beta

Thank you for testing Taveloom. It turns an Android phone into a control surface for a Windows 11 PC (media keys, volume, screenshot,
launch apps/folders/URLs, hotkeys, live CPU/RAM/volume values) and switches the controls automatically based on the app you are using.

> **This is a private beta for invited testers. It is not a public release, it has not been independently security-audited, and it has only
> been tested on a very small number of devices.** Use it on networks and PCs you control and report what you find.

## What's in this folder

| File | What it is |
| --- | --- |
| `Taveloom-Setup-{{VERSION}}-win-x64.exe` | Windows installer (recommended). Unsigned. |
| `Taveloom-{{VERSION}}-win-x64.zip` | Windows portable build (no installer). Unsigned. |
| `Taveloom-{{VERSION}}-android.apk` | Android app, signed with the Taveloom release key. |
| `SHA256SUMS.txt` | Checksums of every file in this folder. **Check them first.** |
| `INSTALL_WINDOWS.md`, `INSTALL_ANDROID.md` | Step-by-step installation and pairing. |
| `TESTER_CHECKLIST.md` | What we would like you to try. |
| `FEEDBACK_TEMPLATE.md` | How to report problems and what to include. |
| `KNOWN_ISSUES.md` | Known limitations. Please read before reporting. |
| `RELEASE_NOTES.md` | What this build contains and what was tested. |
| `PRIVACY.md`, `THIRD_PARTY_NOTICES.md`, `LICENSE`, `NOTICE.md` | Privacy statement, dependency licenses, MIT license, brand notice. |
| `SECURITY.md`, `SUPPORT.md` | Reporting security problems and getting help. |

## Requirements

- **PC:** Windows 11 (64-bit). Windows 10 is not supported.
- **Phone:** Android 7.0 or newer with a camera (for the pairing QR code). Google Play services are not required.
- **Network:** the phone and the PC must be on the **same local network** (same Wi-Fi/router). The connection never leaves your network.

## The short version

1. Verify the files against `SHA256SUMS.txt` (see the install guides).
2. Install on Windows: run the installer, allow Windows Firewall for **private networks only**.
3. Install the APK on the phone.
4. In Taveloom on the PC: **Devices → Pair a phone**. On the phone: **Scan pairing code**. Click **Allow** on the PC.
5. Work through `TESTER_CHECKLIST.md` and send feedback using `FEEDBACK_TEMPLATE.md`.

## Source and signing

- Source commit: `{{SOURCE_COMMIT}}` (repository `biliribrahimefe-crypto/taveloom`).
- Android release certificate SHA-256 (CN=Taveloom Release, OU=Tavrynt Studios):
  `{{CERT_SHA256}}`
- Taveloom is distributed under the Tavrynt Studios brand, which is a brand name and **not** a registered company. Copyright and license: see `LICENSE` and `NOTICE.md`.

## Please do not

- Share these files outside the invited group.
- Post pairing QR codes (or screenshots of them) anywhere; a QR code is a short-lived secret.
- Use Taveloom on networks you do not trust (public Wi-Fi) or let other people use an unlocked, paired phone.

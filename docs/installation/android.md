# Installing Taveloom on Android

Requirements: Android 7.0 (API 24) or newer, a camera, and the same local network as your PC. Google Play services are not required.

> The beta APK is distributed from GitHub, not from Google Play. Taveloom has so far been tested on **one** physical Android phone;
> other devices may behave differently, please report problems.

## Install the APK

1. Download `Taveloom-<version>-android.apk` from the release page on your phone (or copy it over) and compare its SHA-256
   with the release notes if you can.
2. Open it. Android asks to allow installs from that app (browser/files); allow it only for this install.
3. To update, install the newer APK over the old one. This works only when both APKs were signed with the **same key**
   (all official release APKs are). If Android reports a conflict, uninstall first; you will have to pair again.

## Coming from an earlier test build (`dev.efedev.taveloom`)?

The application ID changed to `com.tavrynt.taveloom`. The new app installs **next to** the old one and does not update it; pair the new app
again, revoke the old phone entry on the PC, then uninstall the old app. Details: [package-migration](../release/package-migration.md).

## Pair with your PC

1. On the PC start Taveloom → **Devices → Pair a phone** (the QR code is valid for 2 minutes and works once).
2. On the phone open Taveloom, tap through the intro, name the phone, tap **Scan pairing code**, allow the camera, and scan.
3. A window on the PC asks **Allow** or **Deny**. Check the device name, then Allow. You have up to 60 seconds.
4. Pick a workspace. Taveloom reconnects automatically after Wi-Fi drops, PC restarts or the app returning from the background.

## Troubleshooting

- **"The camera could not start (code)"**: allow Camera for Taveloom in Android settings. If it still fails, report the code in your issue.
- **"Could not reach <PC>"**: same Wi-Fi? Taveloom running? Windows firewall allows private networks? See the
  [Windows guide](windows.md#if-a-phone-cannot-connect).
- **"security certificate did not match"**: the QR code is from a different PC or the PC's identity was reset. Scan a new code.
- **"Access removed"**: the phone was revoked on the PC. Pair again.
- Controls marked *Not set up* need a target configured in Workspace Studio on the PC.

## Remove

Long-press the app → Uninstall. Also revoke the phone on the PC (Devices → Revoke) so its credential stops working there.

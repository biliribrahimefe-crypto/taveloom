# Installing Taveloom on Android

**Requirements:** Android 7.0 (API 24) or newer, a camera, and the same local network as the PC. Google Play services are not required.
The app is distributed as an APK (not through Google Play) and is signed with the Taveloom release key.

## 1. Get the APK onto the phone and verify it

Copy `Taveloom-{{VERSION}}-android.apk` to the phone (USB cable, a private chat to yourself, a cloud drive you trust...).

Optional but recommended: compare its SHA-256 with `SHA256SUMS.txt`. On the PC before copying:
```powershell
Get-FileHash .\Taveloom-{{VERSION}}-android.apk -Algorithm SHA256
```

## 2. Install

1. Open the APK on the phone (Files / Downloads).
2. Android asks to allow installing apps from this source (your file manager or browser). Allow it **for this install**, and switch it off again afterwards
   (the exact menu varies by manufacturer, usually Settings → Apps → Special access → Install unknown apps).
3. Google Play Protect may show a warning because the app is not from Google Play. If you trust the source (the hash matched), choose *Install anyway* / *More details → Install anyway*.
4. Open **Taveloom**.

The Android package is `com.tavrynt.taveloom`. If you ever installed an earlier test build (`dev.efedev.taveloom`), the new app installs **next to it** and does not
replace it; pair the new app again and remove the old one afterwards.

## 3. Pair with the PC

1. On the PC: Taveloom → **Devices → Pair a phone** (QR code valid 2 minutes, usable once).
2. On the phone: tap through the intro, give the phone a name, tap **Scan pairing code**, allow the camera, point it at the QR code.
3. Watch the PC: **Allow this device?** Click **Allow** (up to 60 seconds). The phone then shows your workspaces.

If the phone says *"The camera could not start (code)"*: allow Camera for Taveloom in Android settings (Settings → Apps → Taveloom → Permissions) and tell us the code shown.

## Using it

- Tap the workspace chips to switch; the sparkle icon at the right of the workspace bar turns automatic switching on/off (automatic = follows the focused app on the PC; choosing a workspace by hand pauses it).
- A connection indicator shows Connected / Connecting / Offline. If the PC cannot be reached, taps fail visibly; they never show a fake success.
- Taveloom reconnects by itself after Wi-Fi drops, PC restarts, and when you return to the app.

## Updating

Install the newer APK over the old one. This works because every official APK is signed with the same release key; your pairing is kept.
If Android reports that the app cannot be updated or the signatures do not match, uninstall the old app first (you will need to pair again) and tell us.

## Uninstalling / removing a phone

- Long-press the app icon → Uninstall (removes the phone-side data).
- Also on the PC: Taveloom → Devices → **Revoke** for that phone, so its credential stops working there.
- Without uninstalling: in the app menu choose **Forget this PC**.

## Troubleshooting

| Message | Meaning / fix |
| --- | --- |
| "Could not reach <PC>" | Same Wi-Fi? Taveloom running on the PC? Firewall allows private networks? See `INSTALL_WINDOWS.md`. |
| "...security certificate did not match" | The QR code is from a different PC, or the PC's identity was reset. Scan a new code. |
| "Access removed" / "Not recognised by the PC" | The phone was revoked or the PC data was deleted. Pair again. |
| "Nobody approved the request on the PC in time" | Click Allow within 60 seconds; try again. |
| Controls grey / "Not set up" | The control has no target on the PC. Configure it in Workspace Studio. |
| Stays on "Reconnecting" | Check Wi-Fi; open the app menu → *Reconnect now*. If the PC changed IP, pair again with a fresh QR code. |

See also `KNOWN_ISSUES.md` and `FEEDBACK_TEMPLATE.md`.

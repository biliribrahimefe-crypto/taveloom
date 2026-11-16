# Installing Taveloom on Windows

**Requirements:** Windows 11, 64-bit. No administrator rights needed. About 250 MB of disk space.

## 1. Verify the download

Open PowerShell in this folder and compare each hash with `SHA256SUMS.txt`:
```powershell
Get-FileHash .\Taveloom-Setup-{{VERSION}}-win-x64.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```
The hashes (lower/upper case does not matter) must be identical. If they differ, do not run the file and tell the person who sent it.

## 2. Install (recommended)

1. Run `Taveloom-Setup-{{VERSION}}-win-x64.exe`.
2. **Windows SmartScreen will warn** ("Windows protected your PC") because the installer is **not code-signed yet**. This is expected for this beta.
   If the hash matched, choose **More info → Run anyway**.
3. Follow the wizard. It installs for your user only (`%LOCALAPPDATA%\Programs\Taveloom`), no administrator prompt.
4. Start **Taveloom**. The first time it starts listening, **Windows Firewall asks for permission. Tick "Private networks" only** and leave
   "Public networks" unticked, then click *Allow access*.

If you dismissed the firewall prompt: Windows Security → Firewall & network protection → *Allow an app through firewall* → find Taveloom → tick **Private**.

Your PC's network must use the **Private** profile (Settings → Network & internet → your Wi-Fi → Network profile type → Private).

## Portable alternative

Unzip `Taveloom-{{VERSION}}-win-x64.zip` to any folder and run `Taveloom.exe`. It behaves the same (same warnings, same data folder).

## Pair a phone

1. In Taveloom open **Devices → Pair a phone**. A QR code appears. It is valid for **2 minutes** and works **once**.
2. Install the Android app (`INSTALL_ANDROID.md`) and scan the code.
3. A window appears on the PC: **Allow this device?** Check the device name and click **Allow** (you have up to 60 seconds). **Deny** refuses it.

Manage or remove phones later in **Devices** (Disconnect, **Revoke** = the phone must pair again, Remove from list).

## Updating

Run the newer installer. Settings, workspaces and paired phones are kept (the PC's certificate is preserved, so phones stay paired).

## Uninstalling

Settings → Apps → Installed apps → Taveloom → Uninstall. You are asked whether to also delete settings, workspaces and paired devices.
**No** (default) keeps them for a later reinstall; **Yes** removes `%LOCALAPPDATA%\Taveloom`.

## Troubleshooting

| Symptom | Try |
| --- | --- |
| Phone says "Could not reach <PC>" | Same Wi-Fi? Taveloom running? Firewall allows **private** networks? PC network profile = Private? |
| Works on one Wi-Fi, not another | Guest networks/some routers isolate devices from each other ("AP isolation"). Use the normal network. |
| QR code expired / "pairing code is not valid" | Click *New code* in the pairing window and scan again. |
| PC address changed (new IP) | Open the pairing window again to get a fresh QR code; scan it. |
| Settings → Connection changed | Port and "allow phones" changes need a Taveloom restart and a new QR code. |
| A control shows "Not set up" | Open **Workspace Studio** on the PC and give that control a target (app path, folder, URL...). |
| Hotkey/media key does nothing | Keys cannot reach windows running as administrator. Media keys go to whatever player Windows chooses. |

*Devices → Connection troubleshooting* lists the PC's addresses and whether local-network access is on.
Also see `KNOWN_ISSUES.md` and `FEEDBACK_TEMPLATE.md`.

# Installing Taveloom on Windows

Requirements: Windows 11 (64-bit). Windows 10 has not been tested.

1. Unzip `Taveloom-0.1.0-win-x64.zip` to a folder you keep (for example `C:\Users\you\Apps\Taveloom`). It is self-contained, so no
   .NET install is needed.
2. Run `Taveloom.exe`. The build is unsigned: SmartScreen shows "Windows protected your PC". Choose **More info > Run anyway** only if you
   obtained the file from a source you trust and its SHA-256 matches the one published with the release.
3. On first start Windows Firewall asks whether Taveloom may talk on the network. Allow **private networks**; leave public networks unticked.
4. Open **Devices > Pair a phone**.

Data lives in `%LOCALAPPDATA%\Taveloom` (database, DPAPI-protected certificate). Deleting that folder resets Taveloom and forgets every paired phone.

## If a phone cannot connect

* Phone and PC must be on the same Wi-Fi / LAN, and the PC must not be on a "Public" network profile.
* Devices > *Connection troubleshooting* lists the PC's addresses and whether local-network access is on (Settings > Connection).
* If you changed the port or turned LAN access on, restart Taveloom, then pair again with a fresh QR code.
* Guest Wi-Fi and some routers isolate clients from each other ("AP isolation"); that blocks the connection.

# Installing Taveloom on Windows

Requirements: Windows 11 (64-bit). Windows 10 is not supported/tested. No administrator rights are needed.

## Option A: installer (recommended)

1. Download `Taveloom-Setup-<version>-win-x64.exe` from the release page and check its SHA-256 against the value in the release notes
   (`Get-FileHash .\Taveloom-Setup-*.exe -Algorithm SHA256`).
2. Run it. The installer is **not code-signed**, so Windows SmartScreen shows "Windows protected your PC". Choose
   **More info → Run anyway** only if you downloaded it from the official release page and the hash matches.
3. It installs for the current user into `%LOCALAPPDATA%\Programs\Taveloom` and adds a Start menu entry. The app is self-contained;
   no .NET installation is needed.
4. On first start Windows Firewall asks whether Taveloom may communicate on the network. Allow **private networks** only.

### Updating

Run the newer installer. It replaces the program files in place (a running Taveloom is closed first) and keeps your settings,
workspaces and paired devices. Phones stay paired because the host's certificate is preserved.

### Uninstalling

Settings → Apps → Taveloom → Uninstall. The uninstaller asks whether to also delete settings, workspaces and paired devices.
**No** (the default, also for silent uninstalls) keeps `%LOCALAPPDATA%\Taveloom`, so a later reinstall picks up where you left off.
Choose **Yes** (or run the uninstaller with `/DELETEDATA=1`) to remove everything. If you enabled "start with Windows", that entry is removed too.

## Option B: portable zip

Unzip `Taveloom-<version>-win-x64.zip` anywhere and run `Taveloom.exe`. Data still lives in `%LOCALAPPDATA%\Taveloom`.

## If a phone cannot connect

- Phone and PC must be on the same Wi-Fi/LAN, and the PC's network profile must be **Private**, not Public.
- Taveloom → Devices → *Connection troubleshooting* shows the PC's addresses and whether local-network access is on
  (Settings → Connection). Changing the port or that switch needs a Taveloom restart and a fresh QR code.
- If you dismissed the firewall prompt, allow Taveloom for private networks in *Windows Security → Firewall & network protection →
  Allow an app through firewall*.
- Guest Wi-Fi and some routers isolate devices from each other ("AP isolation"); that blocks the connection.
- Still stuck? See [SUPPORT.md](../../SUPPORT.md).

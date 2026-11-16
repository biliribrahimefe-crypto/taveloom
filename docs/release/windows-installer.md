# Windows installer

Built with [Inno Setup 6](https://jrsoftware.org/isinfo.php) from `installer/taveloom.iss` by `scripts/build-installer.ps1`.

```powershell
winget install JRSoftware.InnoSetup      # once
scripts\build-installer.ps1              # publishes self-contained, then compiles the installer
```
Output: `artifacts\installer\Taveloom-Setup-<version>-win-x64.exe` (about 60 MB). The version comes from `Directory.Build.props`.

## Behaviour

- Per-user install (`PrivilegesRequired=lowest`) into `%LOCALAPPDATA%\Programs\Taveloom`; Start menu entry; optional desktop icon.
- Windows 11 64-bit only (`MinVersion=10.0.22000`).
- Same `AppId` for every version, so installing a newer version upgrades in place. A running Taveloom is closed by the installer.
- Uninstall removes program files and the "start with Windows" entry. It **keeps** `%LOCALAPPDATA%\Taveloom` (settings, workspaces,
  paired devices, certificate) unless the user answers Yes or passes `/DELETEDATA=1`. Silent uninstall keeps data.
- It does not create firewall rules (that would need administrator rights); the app triggers the standard Windows prompt on first start.

## Tested lifecycle

`scripts/test-installer-lifecycle.ps1` installs, upgrades, uninstalls (data kept), reinstalls and uninstalls with data deletion,
using throw-away test installers with a different AppId and data folder, so real installations and data are untouched. The 11 checks
passed for the 0.2.0-beta.1 sources.

Not covered by that test: upgrading while Taveloom is running, installing over a portable-zip copy, Windows 10, and non-English Windows.

## Signing

The installer and `Taveloom.exe` are **unsigned**, so SmartScreen warns on first run. A code-signing certificate (OV/EV or Azure Trusted
Signing) is needed to remove the warning; sign with `signtool` after building, then publish the new SHA-256.
Never commit certificates or `.pfx` files.

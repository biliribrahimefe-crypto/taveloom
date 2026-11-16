; Taveloom Windows installer (Inno Setup 6). Per-user, no administrator rights needed.
; Build with scripts/build-installer.ps1. Defines: MyAppVersion (display), MyAppNumericVersion (x.x.x.x), SourceDir, OutputDir.
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppNumericVersion
  #define MyAppNumericVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\windows-x64"
#endif
; Test hooks (release builds never set these): isolate a test installer from a real installation and from real user data.
#ifndef MyAppId
  #define MyAppId "{{8F3D2B6A-5C41-4E7D-9A0B-6D2E7C1F4A93}"
#endif
#ifndef DataDirName
  #define DataDirName "Taveloom"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
; Never change AppId: it is how upgrades find the existing installation.
AppId={#MyAppId}
AppName=Taveloom
AppVersion={#MyAppVersion}
AppPublisher=Tavrynt Studios
AppPublisherURL=https://github.com/biliribrahimefe-crypto/taveloom
AppSupportURL=https://github.com/biliribrahimefe-crypto/taveloom/issues
AppUpdatesURL=https://github.com/biliribrahimefe-crypto/taveloom/releases
VersionInfoVersion={#MyAppNumericVersion}
VersionInfoProductName=Taveloom
VersionInfoDescription=Taveloom Setup
DefaultDirName={autopf}\Taveloom
DefaultGroupName=Taveloom
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
SetupIconFile=..\apps\windows\Taveloom.App\Assets\taveloom.ico
UninstallDisplayIcon={app}\Taveloom.exe
OutputDir={#OutputDir}
OutputBaseFilename=Taveloom-Setup-{#MyAppVersion}-win-x64
#ifdef FastTest
Compression=none
#else
Compression=lzma2/max
#endif
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Taveloom"; Filename: "{app}\Taveloom.exe"
Name: "{autodesktop}\Taveloom"; Filename: "{app}\Taveloom.exe"; Tasks: desktopicon

[Registry]
; The app can register itself to start with Windows; remove that entry when uninstalling.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Taveloom"; ValueType: none; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\Taveloom.exe"; Description: "Launch Taveloom"; Flags: nowait postinstall skipifsilent

[Code]
// Program files are always removed on uninstall. User data (settings, workspaces, paired devices, certificate) lives in
// %LOCALAPPDATA%\{#DataDirName} and is KEPT unless the user says yes (or passes /DELETEDATA=1 to the uninstaller).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DeleteData: Boolean;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteData := ExpandConstant('{param:DELETEDATA|0}') = '1';
    if (not DeleteData) and (not UninstallSilent) then
      DeleteData := MsgBox('Also delete Taveloom settings, workspaces and paired devices?' + #13#10 +
        'Choose No to keep them for a later reinstall.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    if DeleteData then
      DelTree(ExpandConstant('{localappdata}\{#DataDirName}'), True, True, True);
  end;
end;

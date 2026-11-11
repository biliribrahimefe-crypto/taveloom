# Building and testing

## Windows (host, app, tests)

Prerequisites: Windows 11, .NET 10 SDK.

```bash
dotnet restore Taveloom.sln
dotnet build Taveloom.sln -c Release
dotnet test Taveloom.sln -c Release
dotnet format Taveloom.sln --verify-no-changes
```

Tests include integration tests that start a real `TaveloomHost` on an ephemeral loopback port and talk to it with a pinning
WebSocket client, using fake input / audio / foreground back-ends. `NativeSmokeTests` call the real Windows telemetry and
screenshot code.

### Running the app against throwaway data

```powershell
$env:TAVELOOM_DATA_DIR = "$env:TEMP\taveloom-dev"   # separate database and certificate
$env:TAVELOOM_LAN = "0"                              # loopback only: no firewall prompt, no phone access
$env:TAVELOOM_PAGE = "Studio"                        # optional: open on Dashboard|Devices|Studio|Settings
dotnet run --project apps/windows/Taveloom.App
```

`scripts/capture-window.ps1` uses the same switches to take screenshots.

### Publishing

```bash
dotnet publish apps/windows/Taveloom.App -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
```

Produces an unsigned, self-contained folder (~200 MB). There is no installer yet.

> If `dotnet`-built apps report "You must install or update .NET", a user-level install is shadowing the machine one;
> set `DOTNET_ROOT` to `C:\Program Files\dotnet` or use the self-contained publish.

## Android

Prerequisites: Flutter (stable, Dart 3.7+), Android SDK with platform tools, JDK 17+. Use **JDK 17-21** (Gradle 9 rejects newer ones such as 26).

```bash
cd apps/android
flutter create --platforms=android --org dev.efedev --project-name taveloom .   # once; commit the generated android/ folder
# add CAMERA permission, set minSdkVersion 23+  (see apps/android/README.md)
flutter pub get
flutter analyze
flutter test
flutter build apk --release
```

`test/protocol_test.dart` asserts the same HMAC vector as the C# tests, so the two implementations stay compatible.

### Verifying end to end (still to be done)

1. Run Taveloom on Windows with LAN access on; allow the firewall prompt for private networks.
2. Install the APK on a phone on the same Wi-Fi, scan the QR, click Allow.
3. Press a media button and confirm the Windows action happens and the phone shows the real result.
4. Kill the network / restart Taveloom and confirm the phone shows reconnecting, then recovers.
5. Revoke the phone on Windows and confirm it is disconnected and cannot reconnect.

### Android build gotchas seen on Windows

* Run Flutter/Dart tools from a path with **ASCII characters only**: the Dart analysis server crashes on paths that contain non-ASCII letters (for example a localized "Desktop" folder name).
  Mirror `apps/android` to a plain path (for example `C:\dev\tav-android`) and sync changes back.
* If the build prints "Error when reading ../../AppData/Local/Pub/Cache/...", set `PUB_CACHE` to a folder near the project
  (for example `C:\dev\pub-cache`) and run `flutter pub get`.
* `storage.googleapis.com` can be slow; the first `flutter` run downloads ~220 MB of Dart SDK there.
* The first APK build downloads the NDK, CMake and platform 35 (several GB, ~6 min after downloads).

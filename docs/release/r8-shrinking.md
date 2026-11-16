# R8 code shrinking: decision and evidence

**Decision for 0.2.0-beta: R8 shrinking stays OFF for release builds.** This is the configuration that was tested on a physical phone.

## What we know

| Build | R8 | Result on the physical phone |
| --- | --- | --- |
| v0.1.0 APK | on (Flutter default for release) | QR scanner screen: "camera is not available" although camera permission was granted |
| 0.2.0 beta APK | off | QR scanning works; pairing, controls, reconnection pass |

Between those two builds the only functional change to the scanner path was turning R8 off (and showing the real error code, which
was never captured because the failing build was not run again). R8 shrinking is a well-known cause of this kind of failure
(reflection in ML Kit / CameraX behind `mobile_scanner`), so R8 is the **most likely** cause, but this is **correlation, not a
proven root cause**: the original failure's error code was not recorded.

A build with R8 **on** (`TAVELOOM_R8=1`) from the same sources compiles cleanly: R8 emits no `missing_rules.txt`, and the
scanner classes are present in the mapping. That shows the build is consistent; it does **not** show the camera works.
It has not been run on a device.

## Trade-off of leaving R8 off

- The APK is about 3.6 MB larger (68.1 MB vs 64.5 MB measured) and the code is not obfuscated.
- The project is open source, so obfuscation has little value. Size is the only real cost.

## How to enable it later (only with device proof)

1. `set TAVELOOM_R8=1` (PowerShell: `$env:TAVELOOM_R8="1"`) and run `flutter build apk --release`.
2. Install on a phone. Test: scan a QR code, pair, press controls, background/resume.
3. If the scanner fails, capture the code shown on screen and `adb logcat` output, then add keep rules to
   `android/app/proguard-rules.pro` for the classes named in the log and repeat.
4. Only when all steps pass, make R8 the default in `android/app/build.gradle.kts` and update this document.

Until then, do not describe shrinking as "fixed" or "safe".

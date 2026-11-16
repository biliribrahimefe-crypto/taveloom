# Android package migration: `dev.efedev.taveloom` → `com.tavrynt.taveloom`

## What changed
`applicationId` and `namespace` (and the Kotlin package of `MainActivity`, now `android/app/src/main/kotlin/com/tavrynt/taveloom/`).
The Dart package name (`taveloom`), the app label ("Taveloom"), icons and all protocol behaviour are unchanged.

## What this means for installs
Android identifies an app by its application ID. The new ID is a **different app**:

- It installs **next to** the old `dev.efedev.taveloom` build; neither replaces the other. They keep separate data.
  (Nothing in the app uses fixed content-provider authorities or shared user IDs that could clash; the generated permission name
  is derived from the application ID, so it differs too.)
- There is no in-place update from the old app. Data is not carried over: **pair the new app again**.
- On the PC the old phone entry stays in *Devices* as offline. Remove it (Revoke, then *Remove from list*) once the new one works.
- The two apps would both try to reconnect to the same PC while both are installed and paired. Uninstall the old app when done.

Recommended migration for testers:
1. Install the new app (it does not touch the old one).
2. Pair it (Devices → Pair a phone). Test.
3. In Taveloom on the PC: revoke the old phone entry.
4. Uninstall the old `dev.efedev.taveloom` app.

## Why now
No APK was ever published to the public; the only distributed artifact of 0.1.0 was the Windows zip. Changing the ID later, after users
or a Play listing exist, would force every user through a reinstall.

## What did not change
- Windows: installer `AppId`, install location, `%LOCALAPPDATA%\Taveloom` data, pairing credentials, certificate. Existing Windows
  installations upgrade in place.
- Protocol and wire format (no migration needed between app versions).

## Signing identity
The release signing key is created after the ID is final (see [android-signing.md](android-signing.md) and
[google-play-and-signing.md](google-play-and-signing.md)). The old ID was only ever signed with debug keys.

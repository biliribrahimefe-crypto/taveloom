# Google Play App Signing vs direct APK distribution

Android only installs an update when the new APK has the **same application ID and is signed by the same key** as the installed one.
Taveloom can be distributed two ways; the way keys are handled decides whether users can move between them.

## Direct APK (GitHub releases)

- You sign the APK with **your release key** (`taveloom-release.jks`, created by `scripts/new-android-keystore.ps1`).
- Every future direct APK must use that same key, or users must uninstall and re-pair.
- Losing the key or its password means you can never update installed copies. Back it up (see below).

## Google Play with Play App Signing (default and recommended by Google)

- Google holds the **app signing key**. You sign each upload (AAB) with a separate **upload key**; Google re-signs what users download.
- If the upload key is lost or leaked, you can ask Google to reset it. The app signing key is not at risk.
- **Compatibility consequence:** an APK downloaded from Play is signed by Google's app signing key. A GitHub APK signed by *your* release key
  has a different signature, so **the two channels cannot update each other** (Android reports a signature conflict; users must uninstall,
  and then re-pair).

## Using one key for both channels

When you create the app in the Play Console you can choose to **upload your own existing key** as the app signing key
(Google documents this using its PEPK tool) instead of letting Google generate one. Then Play-installed and GitHub-installed copies share one
signature and update each other. This choice is only available at app creation / first upload; decide before uploading anything.
Trade-off: the key's security then depends on you (a leak is harder to recover from) and the export step handles the private key, so do it
only on a trusted machine and never paste it anywhere.

| Goal | Recommendation |
| --- | --- |
| Beta/GitHub only for now | Create the release key now, sign APKs with it, keep it forever. |
| Later add Play, and want one signature | At first Play upload choose *upload an existing key* (your release key). |
| Later add Play, separate channels are fine | Let Google generate the signing key; use a *different* upload key. Document that channels do not update each other. |

## Key custody rules

1. Create the key once, on a trusted machine, with `scripts/new-android-keystore.ps1` (the script prompts for the password; nobody else sees it).
2. Keep **two backups** of the `.jks` (offline USB + encrypted cloud/password-manager attachment) and the password in a password manager.
3. Never commit, paste or share the keystore, `key.properties` or passwords (they are git-ignored; check `git status` before every commit).
4. Record the **certificate SHA-256** (`apksigner verify --print-certs`) in release notes so people can verify downloads. It is public information.
5. Rehearse recovery once: restore the backup on another folder and run `keytool -list -keystore <file>` to confirm it opens.
6. If the key may have leaked: stop distributing; with Play App Signing request an upload-key reset; for direct APKs you must create a new key
   and every user must reinstall.

## Checklist before the first signed release

- [ ] Application ID is final (`com.tavrynt.taveloom`).
- [ ] Release key created and **backed up twice**; recovery rehearsed.
- [ ] Decision recorded: GitHub only / Play with Google-held key / Play with your own key.
- [ ] `apksigner verify --print-certs` on the release APK shows the release certificate (not "Android Debug").
- [ ] Certificate SHA-256 published in the release notes.

# Tester checklist ({{VERSION}})

Tick what you tried and note anything surprising in the feedback form (`FEEDBACK_TEMPLATE.md`). Partial runs are welcome.
**Before you start:** hashes verified, Windows firewall allowed for *private networks*, phone and PC on the same Wi-Fi.

## A. Install
- [ ] Windows installer ran (SmartScreen warning appeared / did not appear)
- [ ] Android APK installed (Play Protect warning appeared / did not appear)
- [ ] App opens on both devices. Version: Windows sidebar footer / Settings → About should show `{{VERSION}}`; Android: system Settings → Apps → Taveloom → App info shows `{{ANDROID_VERSION}}`

## B. Pairing
- [ ] QR code appears on the PC; the phone camera opens and scans it
- [ ] Approval prompt appears on the PC with the right device name; **Allow** → phone shows workspaces
- [ ] Wait ~30 seconds before clicking Allow → pairing still succeeds
- [ ] **Deny** → phone shows a clear "declined" message; no device appears in the PC list
- [ ] Do not click anything for 70 seconds → the PC prompt closes and the phone shows that the request was declined / timed out
- [ ] Scan the same QR code twice → the second attempt is refused
- [ ] Wait 3 minutes, then scan an old QR code → "expired" message

## C. Controls
- [ ] Play/Pause (with a player open) · Next · Previous
- [ ] Volume slider changes the Windows volume; Mute toggles; values on the phone match the PC
- [ ] Screenshot (find the file in `Pictures\Taveloom` on the PC; **delete it if it shows private content**)
- [ ] CPU / memory value tiles show plausible numbers (or "unavailable", never a fake number)
- [ ] A control with no target shows *Not set up*; give it one in Workspace Studio and it works

## D. Workspaces
- [ ] Switch workspaces from the phone and from the PC dashboard
- [ ] Automatic switching: focus VS Code / a browser / a media player on the PC; the phone follows (if you created rules)
- [ ] Edit a workspace in Workspace Studio (rename, move a tile, add a control); the phone updates live
- [ ] Manual choice pauses automatic switching; the sparkle icon resumes it

## E. Resilience
- [ ] Turn phone Wi-Fi off and on → phone shows Reconnecting, then Connected; no control claims success while offline
- [ ] Put the app in the background for a minute, return → reconnects
- [ ] Restart Taveloom on the PC → phone reconnects by itself
- [ ] Reboot the PC → after Taveloom starts, the phone reconnects (no new pairing needed)
- [ ] Switch the phone to mobile data → shows Offline/Reconnecting (not connected)

## F. Device management and security
- [ ] **Revoke** the phone on the PC → phone disconnects and says access was removed; reconnecting is refused
- [ ] Pair the same phone again afterwards → works
- [ ] **Forget this PC** in the phone menu → phone returns to the intro

## G. Install lifecycle (Windows)
- [ ] Run the installer again (upgrade/reinstall) → phone stays paired, workspaces kept
- [ ] Uninstall, choosing to keep data; reinstall → everything still there
- [ ] (Optional) Uninstall choosing to delete data → fresh start

## H. Anything else
- [ ] Layout/readability on your phone (portrait and landscape), dark/light theme on both devices
- [ ] Anything confusing in wording, error messages or the first-run experience

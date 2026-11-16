# Release checklist

Run in order. Do not tag or publish until every box is ticked or the exception is written down in the release notes.

**Code**
- [ ] Version bumped in `Directory.Build.props` and `apps/android/pubspec.yaml` (increase `versionCode`).
- [ ] `dotnet format Taveloom.sln --verify-no-changes`, `dotnet build -c Release` (0 warnings), `dotnet test -c Release`.
- [ ] `dart format --set-exit-if-changed lib test`, `flutter analyze`, `flutter test` (ASCII-only path).
- [ ] `scripts/e2e-android-vs-host.ps1 -AndroidDir <ascii path>` passes.
- [ ] GitHub Actions green on the release commit (check that jobs actually ran: open each step list).
- [ ] `git status` clean; no keystore, `key.properties`, `.pfx`, `.db` staged; secret scan of the diff.

**Artifacts** (build from the committed release commit)
- [ ] `scripts/build-installer.ps1` → installer; zip of `artifacts/windows-x64`.
- [ ] Installer lifecycle test (`scripts/test-installer-lifecycle.ps1` with test installers).
- [ ] Android: release key present (`docs/release/android-signing.md`); `flutter build apk --release` and `appbundle`; `apksigner verify --print-certs` shows the release certificate, not "Android Debug".
- [ ] `SHA256SUMS.txt` generated from the final files; hashes pasted in the release notes.

**Device test** (physical phone, see `docs/development/building.md`)
- [ ] Pair (immediate, 25 s delayed, rejected, expired), controls, Wi-Fi drop, PC restart, background/resume, revoke, re-pair.
- [ ] Windows installer on a clean machine/user: first run, firewall prompt, upgrade, uninstall (keep data).

**Docs**
- [ ] README status and "What was tested" match reality; CHANGELOG; release notes; PRIVACY, SUPPORT, THIRD_PARTY_NOTICES current.

**Publish** (needs explicit approval)
- [ ] Merge PR, tag `vX.Y.Z-beta.N`, create the GitHub release as a *pre-release*, attach artifacts and `SHA256SUMS.txt`.

# Notice

**Taveloom** is distributed under the **Tavrynt Studios** brand.

- *Tavrynt Studios* is a brand name used for publishing Taveloom. It is **not** a registered company or other legal entity, and nothing in
  this repository should be read as a claim that it is.
- Taveloom was originally published under the name **EFEDEV**. The original copyright notice is preserved unchanged in
  [LICENSE](LICENSE): `Copyright (c) 2026 EFEDEV`.
- The software remains licensed under the **MIT License** (see [LICENSE](LICENSE)). Distributing it under the Tavrynt Studios brand does not
  change the license, the copyright holder named there, or the notices of third-party components in
  [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
- Contributions are accepted under the same MIT License.
- "Taveloom" and "Tavrynt Studios" are names used by the project; no trademark is registered or claimed.

## Identifier changes you may notice

| Item | Before | Now |
| --- | --- | --- |
| Android application ID / namespace | `dev.efedev.taveloom` | `com.tavrynt.taveloom` |
| Publisher shown in the Windows installer and app | EFEDEV | Tavrynt Studios |

The Windows installer `AppId` and the data folder `%LOCALAPPDATA%\Taveloom` are unchanged, so existing Windows installations
upgrade in place and keep their data. See [docs/release/package-migration.md](docs/release/package-migration.md) for the Android migration.

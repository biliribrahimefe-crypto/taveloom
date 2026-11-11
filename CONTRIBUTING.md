# Contributing

Thanks for helping. Taveloom controls a real PC, so correctness and honesty matter more than feature count.

## Ground rules

* No fake functionality: a control either does what it says or reports that it cannot.
* The phone sends control ids, never commands. Do not add any path that executes data received from the phone.
* Anything touching pairing, authentication, actions or the protocol needs a test, including a failure-path test.
* Keep it small and readable. Prefer the existing structure to a new abstraction.

## Setup

* Windows 11, .NET 10 SDK, Visual Studio / Rider / VS Code.
* For Android work: Flutter (stable) and the Android SDK. See [docs/development/building.md](docs/development/building.md).

```bash
dotnet build Taveloom.sln
dotnet test Taveloom.sln
dotnet format Taveloom.sln          # must leave no changes; CI enforces it
```

Android: `flutter analyze && flutter test && dart format lib test` in `apps/android`.

## Pull requests

1. Open an issue first for anything large.
2. Branch from `main`, keep PRs focused, and fill in the template (what changed, how you verified it, security impact).
3. Add or update tests. Do not weaken or skip existing ones.
4. If you change the wire protocol, update `docs/architecture/protocol.md`, both implementations, and the shared test vectors.

## Reporting security issues

Do not open a public issue. See [SECURITY.md](SECURITY.md).

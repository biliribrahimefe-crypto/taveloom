# Security policy

Taveloom controls a real computer, so security reports are taken seriously. The project has **not** had an independent security audit.

## Reporting a vulnerability

Please do **not** open a public issue. Use GitHub's private vulnerability reporting on this repository
(Security tab, "Report a vulnerability"). If that is unavailable, contact the maintainer, EFEDEV, through the profile listed on the repository.

Include what you found, how to reproduce it, and what an attacker gains. You can expect an acknowledgement within a few days;
this is a small project, so there is no formal SLA.

## Scope

In scope: authentication or pairing bypass, getting the host to run anything that is not a configured control, credential or key
exposure, TLS pinning bypass, privilege or path issues in launchers, denial of service by an unauthenticated peer.

Out of scope: an attacker who already has an approved, unlocked phone or who runs code as your Windows user.

See [docs/security/security-model.md](docs/security/security-model.md) for the threat model and known residual risks.

## Supported versions

Only the latest release.

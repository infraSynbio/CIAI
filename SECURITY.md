# Security policy

## Supported versions

CIAI is currently a public preview. Security fixes are applied to the latest `2.0.x` preview branch. Older snapshots are not supported.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting/security advisory feature for this repository. Do not open a public Issue containing an exploit, credential, certificate, device address, vendor SDK, sample identifier, or production log.

Include the affected SDK/language, version, deployment mode, reproduction steps, impact, and any safe proof of concept. Maintainers should acknowledge a complete report within five business days and coordinate disclosure after a fix is available.

## Deployment baseline

- HTTP is enabled by default only for local development and controlled integration networks.
- Use HTTPS for cross-host or production traffic; use mTLS when device-control policy requires mutual identity.
- Store certificate and trust-store passwords in environment variables or an external secret manager.
- Never commit PFX/P12/JKS files, private keys, access tokens, real device addresses, or vendor binaries.
- Restrict listener interfaces, ports, callback destinations, adapter executable paths and file-workflow roots.
- Run drivers with the least OS privileges required by the vendor SDK.
- Treat device commands as safety-relevant; CIAI does not replace physical interlocks, emergency stops or vendor safety controls.

## Historical credential notice

Repository snapshots that predate the `2.0.0-beta.1` clean-root release contained example PFX/CER files, generated output and vendor-oriented samples. The 2.0 public history starts from a scanned clean tree and obsolete public branch references were removed. This prevents the current repository refs from redistributing those files, but it cannot erase existing forks, clones, caches or downloaded archives.

Every private key from an earlier snapshot must still be treated as compromised:

1. never reuse an earlier example key or certificate;
2. revoke or rotate it if it was ever trusted by a real deployment;
3. remove old clones and build caches, then clone the clean history again;
4. scan every future release tree for secrets, generated output, vendor binaries and restricted documentation.

The repository must never publish a password that makes a historical private key usable.

## Scope

Security reports may cover HTTP handling, TLS/mTLS validation, certificate binding, YAML/environment expansion, callbacks, path traversal, process framing, request limits, concurrency, idempotency and denial of service. Device-vendor vulnerabilities should also be reported to the relevant vendor.

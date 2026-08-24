# Changelog

All notable changes are documented here. CIAI follows semantic versioning for SDK packages; the wire protocol has its own major-version compatibility rules.

## 2.0.0-beta.1 — 2026-08-24

### Added

- Aligned .NET and Java protocol behavior for the seven CIAI endpoints.
- Declarative YAML initialization, typed vendor settings and path-aware diagnostics.
- Named connections, resource groups and custom communication providers.
- TCP/serial transaction serialization and multi-byte framing helpers.
- HTTP methods, headers and binary payload support for device APIs.
- Bounded Function queues, `instructionId` idempotency, HTTP 429 and graceful shutdown.
- Dynamic Nest providers, cancellation/progress/events and file-workflow safety helpers.
- Length-prefixed process adapter support for legacy DLL/COM integrations.
- Contract tests, CI, OpenAPI/JSON Schema, clean vendor-neutral examples and open-source governance files.
- English project and wire-protocol documentation, structured issue forms and dependency update automation.
- Backend-neutral Java logging; applications choose their SLF4J implementation instead of inheriting file logging from the SDK.

### Changed

- Local samples default to HTTP; HTTPS/mTLS remains available through commented configuration templates.
- Certificate passwords and tokens are represented by environment variables.
- Public samples no longer include compiled output, certificates or vendor-specific binaries.

### Fixed

- YAML environment placeholders inside comments no longer break an HTTP-only configuration.
- Initialization, heartbeat and Set/Result failure semantics are consistent across .NET and Java.
- Java heartbeat timestamps now include an RFC 3339 UTC offset, matching .NET and OpenAPI.
- Portable TLS defaults now use TLS 1.2 on Java 8/.NET, unsupported Java protocol or cipher settings fail at startup, and disabled client authentication no longer creates a trust-all manager.
- Runtime, YAML, JSON, serial, logging and test dependencies were refreshed while retaining .NET 6/.NET 8 and Java 8 compatibility.

### Security

- The public repository was reset to this scanned clean root and the obsolete public branch was removed, so current refs no longer redistribute historical example certificates or generated/vendor binaries.
- Historical example private keys remain compromised wherever an older clone, fork, cache or archive exists; they must never be reused and must be rotated if they were ever trusted. See `SECURITY.md`.

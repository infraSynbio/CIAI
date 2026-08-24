## Summary

Describe the externally observable behavior and why the change is needed.

## Compatibility

- [ ] The .NET and Java behavior remains aligned, or the platform-specific difference is documented.
- [ ] OpenAPI, JSON Schema, protocol docs, examples, and changelog are updated when applicable.
- [ ] No standard endpoint was added or changed without a protocol proposal.

## Verification

- [ ] .NET SDK builds with zero errors.
- [ ] .NET contract tests pass.
- [ ] `mvn --batch-mode --no-transfer-progress verify` passes.
- [ ] Real vendor transport/hardware gaps are explicitly listed as unverified.

## Repository safety

- [ ] This change contains no generated output, credential, certificate, real device address, vendor binary, restricted document, production log, or sensitive laboratory data.
- [ ] Any third-party code or asset has a redistributable license and attribution.

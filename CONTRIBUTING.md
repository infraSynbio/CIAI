# Contributing to CIAI

Thank you for improving open laboratory automation interoperability.

## Before opening a pull request

1. Open an Issue for protocol-breaking changes or large new capabilities.
2. Keep the wire behavior equivalent in .NET and Java unless the difference is an unavoidable platform constraint.
3. Add or update contract tests before changing protocol behavior.
4. Do not add `/FunctionSync` or another public endpoint without a new protocol proposal and conformance tests.
5. Do not commit generated output, IDE files, certificates, secrets, production logs, vendor DLLs or restricted vendor documentation.

## Development

.NET:

```powershell
dotnet build CiaiControllerSDK/CiaiControllerSDK.csproj -c Release
dotnet run --project CiaiControllerSDK.ContractTests/CiaiControllerSDK.ContractTests.csproj -c Release
dotnet build examples/csharp-temperature/Ciai.Example.Temperature.csproj -c Release
```

Java:

```bash
./mvnw --batch-mode --no-transfer-progress verify
```

## Driver contributions

A device driver contribution must include:

- a mapping from the device document/API to every public CIAI member;
- a license statement proving that contributed code and assets may be redistributed;
- no vendor binary unless its license explicitly permits repository redistribution;
- simulation tests and an explicit list of hardware tests not performed;
- device-state evidence for success, failure, cancellation and recovery;
- precise transport framing/concurrency tests when applicable.

Drivers requiring proprietary dependencies should normally live in their own repository and consume this SDK.

## Compatibility

- Additive optional JSON fields may be introduced in a minor release.
- Breaking endpoint, method, field type or behavior changes require a new protocol major version.
- Public API changes should carry a migration note in `CHANGELOG.md`.
- Unknown vendor settings remain allowed; public diagnostics must not impose a closed schema on every vendor.

## Contribution license

Unless explicitly stated otherwise, contributions intentionally submitted to this project are licensed under Apache-2.0 as described in the repository `LICENSE`.

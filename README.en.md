# CIAI

English | [简体中文](README.md)

[![CI](https://github.com/infraSynbio/CIAI/actions/workflows/ci.yml/badge.svg)](https://github.com/infraSynbio/CIAI/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Protocol](https://img.shields.io/badge/CIAI-2.0--preview-orange.svg)](docs/protocol.en.md)

CIAI (Comprehensive Interface for Autonomous Integration) is an open device-driver protocol and SDK for biological laboratory automation. It gives orchestration systems seven consistent HTTP endpoints for discovering and controlling instruments while keeping vendor-specific TCP, serial, HTTP, modern DLL/API, and legacy DLL/COM details inside each driver.

> The current release is `2.0.0-beta.1`. It is suitable for integration validation; wire and SDK compatibility may still change before the first stable release.

## Why CIAI

- Exactly seven standard endpoints: `/Info`, `/HeartBeat`, `/Function`, `/Operation`, `/Set`, `/Get`, and `/EnterAndExit`.
- Aligned .NET and Java wire JSON, error semantics, and asynchronous Function callback behavior.
- Declarative YAML startup: ordinary TCP, serial, and HTTP drivers do not need custom connection factories, connection lifecycle code, or semaphores.
- Independent limits for business capabilities, Function resources, vendor DLL/API calls, and physical connections.
- Transaction serialization for single-channel TCP and serial request/response protocols.
- Named connections, shared resource groups, custom communication providers, dynamic Nests, typed vendor settings, cancellation, progress, events, and file workflows.
- A length-prefixed process bridge for x86, .NET Framework, COM, STA, or otherwise incompatible vendor runtimes.
- Bounded Function queues, `instructionId` idempotency, HTTP 429 backpressure, request-size limits, and graceful shutdown in the host.

## Repository layout

```text
CiaiControllerSDK/                 .NET 6/.NET 8 SDK
CiaiControllerSDKForJava/          Java 8-compatible SDK
CiaiControllerSDK.LegacyAdapter/   Legacy DLL/COM process bridge protocol
CiaiControllerSDK.ContractTests/   .NET contract tests
.agents/skills/                    Reusable driver development and migration skill
examples/                           Runnable vendor-neutral examples
docs/                               Protocol, API, configuration, and conformance docs
schemas/                            JSON Schema
openapi/                            OpenAPI 3.1 description
```

The repository intentionally contains no vendor SDK binaries, production drivers, private keys, access tokens, or generated build output.

## Five-minute start

### .NET

Install the .NET 8 SDK, then run:

```powershell
dotnet run --project examples/csharp-temperature/Ciai.Example.Temperature.csproj
```

Open `http://127.0.0.1:18080/Info`.

### Java

Install JDK 8+ and Maven 3.8+, then run:

```bash
./mvnw -pl examples/java-temperature -am install
./mvnw -f examples/java-temperature/pom.xml exec:java
```

Open `http://127.0.0.1:18081/Info`. The two examples publish the same device capabilities so the language implementations are easy to compare.

## Minimal driver

The host and communication lifecycle come from `application.yml`; the driver does not need a constructor.

```csharp
[DeviceDriver("Temperature device", FunctionalResources = 1)]
public sealed class TemperatureDriver : DeviceDriverBase
{
    [DeviceFunction("run", TitleCN = "运行", TitleEN = "Run")]
    public Task<Result<Finish>> Run(FunctionData data)
    {
        var parameter = RequireFunctionParam<RunParameter>(data);
        ExecutionCancellationToken.ThrowIfCancellationRequested();
        ReportProgress(10, "Command accepted by device");
        return Task.FromResult(Result<Finish>.Success(Finish.Success()));
    }
}
```

```java
@DeviceDriver(name = "Temperature device", functionalResources = 1)
public final class TemperatureDriver extends DeviceDriverBase {
    @DeviceFunction(name = "run", titleCN = "运行", titleEN = "Run")
    public Result<Finish> run(FunctionData data) {
        RunParameter parameter = requireFunctionParam(data, RunParameter.class);
        getCurrentExecution().throwIfCancellationRequested();
        reportProgress(10, "Command accepted by device", null);
        return Result.success(Finish.success());
    }
}
```

See [examples](examples/README.md) for complete runnable drivers.

## Declarative configuration

HTTP is the local-development default. HTTPS and mTLS remain available through the fully commented templates in both SDKs.

```yaml
server:
  host: "0.0.0.0"
  port: 12345
  useHttps: false

callback:
  enabled: false
  url: ""

device:
  deviceId: "my-device-001"
  communicationType: "Serial"
  serial:
    port: "COM3"
    baudRate: 9600
    dataBits: 8
    stopBits: 1
    parity: "none"
    readTimeoutMs: 5000
    writeTimeoutMs: 5000
  settings:
    vendor:
      station: 1
```

Use `${ENVIRONMENT_VARIABLE}` for certificate or trust-store passwords. Never commit certificates, passwords, real device addresses, or vendor binaries.

## Independent resource dimensions

| Setting or attribute | Purpose | Controls a serial/TCP lock |
|---|---|---|
| `FunctionalResources` | Concurrent Function business resources | No |
| `Parallelizability` | Business parallelism advertised upstream | No |
| `deviceCallResources` | Real vendor DLL/API call resources | No |
| `connections.*.maxConcurrency` | Per-connection physical resources | Yes |

TCP, serial, and Modbus-like request/response links are always forced to one transaction. Increase HTTP, process, DLL, or API concurrency only when the vendor explicitly supports multiple independent sessions.

## Standard endpoints

| Method | Path | Behavior |
|---|---|---|
| GET | `/Info` | Registration, capabilities, Nests, and form descriptions |
| GET | `/HeartBeat` | Driver and device health |
| POST | `/Function` | Accept a long-running task and optionally callback on completion |
| POST | `/Operation` | Synchronous operation |
| POST | `/Set` | Set one or more parameters |
| GET | `/Get` | Read all public state |
| POST | `/EnterAndExit` | Load, unload, or transfer entry point |

There is no `/FunctionSync`. Cancellation, progress, and proactive events are SDK host extensions and do not create an eighth CIAI 2.0 endpoint.

## Documentation

- [CIAI 2.0 wire protocol](docs/protocol.en.md)
- [OpenAPI 3.1](openapi/ciai-2.0.yaml)
- [Wire-message JSON Schema](schemas/ciai-2.0.schema.json)
- [`application.yml` JSON Schema](schemas/application.schema.json)
- [Configuration and communication](docs/configuration.md) (Chinese)
- [API reference](docs/api-reference.md) (Chinese)
- [Conformance](docs/conformance.md) (Chinese)
- [.NET SDK guide](CiaiControllerSDK/README.md) (Chinese)
- [Java SDK guide](CiaiControllerSDKForJava/README.md) (Chinese)
- [Security policy](SECURITY.md)
- [Contributing](CONTRIBUTING.md)
- [AI driver development and migration skill](.agents/skills/device-driver-migration/SKILL.md) (Chinese)

## Build and test

```powershell
dotnet build CiaiControllerSDK/CiaiControllerSDK.csproj -c Release
dotnet run --project CiaiControllerSDK.ContractTests/CiaiControllerSDK.ContractTests.csproj -c Release
dotnet build examples/csharp-temperature/Ciai.Example.Temperature.csproj -c Release
```

```bash
./mvnw --batch-mode --no-transfer-progress verify
python -m pip install -r scripts/requirements-validation.txt
python scripts/validate_contracts.py
python .agents/skills/device-driver-migration/scripts/validate_skill.py
```

Contract tests do not require hardware. Vendor SDK, certificate, transport, and real-device behavior must still be validated in each driver project; simulation is not hardware evidence.

## License

Code and documentation are licensed under the [Apache License 2.0](LICENSE); see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for direct dependency attribution. Vendor SDKs, trademarks, protocol documents, and binaries remain subject to their owners' terms; Apache-2.0 does not grant redistribution rights for them.

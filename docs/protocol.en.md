# CIAI Device Integration Protocol 2.0

[简体中文](protocol.md) | English

Status: public preview (`2.0.0-beta.1`)

This document defines the language-independent wire protocol between a CIAI driver and an upstream orchestration system. A C#, Java, or other implementation is compatible when it satisfies this document and the published schemas.

The terms **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** describe mandatory, prohibited, recommended, discouraged, and optional behavior.

## 1. Scope

CIAI specifies device discovery, health checks, long-running functions, synchronous operations, parameter writes, state reads, and sample or labware transfer. It does not define a vendor's internal device protocol and does not replace physical interlocks, laboratory biosafety procedures, or vendor operating instructions.

## 2. Versioning

- The current protocol major version is 2.
- A `2.x` release may add optional fields, optional enum values, or capabilities that do not change existing behavior.
- Removing a field or changing its type, endpoint method, or success/failure semantics requires a new major version.
- CIAI 2.0 does not perform wire-level version negotiation. Compatibility is identified by release versions and conformance statements.
- `/Info` `basicInfo.version` is the device-driver version, not a protocol negotiation field.
- Receivers SHOULD ignore unknown JSON fields but MUST NOT ignore an invalid type for a known field.

## 3. Transport

- A driver MUST provide HTTP/1.1 or HTTPS.
- JSON MUST use UTF-8.
- Endpoints with a request body MUST accept `Content-Type: application/json`.
- HTTP MAY be used for local development. Cross-host, production, or untrusted-network deployments SHOULD use HTTPS; sensitive control environments SHOULD use mTLS.
- Implementations MUST NOT log certificate passwords, tokens, complete sensitive sample information, or unnecessary raw data.

## 4. Fixed endpoints

A compatible implementation MUST expose the following seven paths as the CIAI 2.0 standard endpoints:

| Method | Path | Request | Response data |
|---|---|---|---|
| GET | `/Info` | None | `RegisterInfo` |
| GET | `/HeartBeat` | None | `HeartBeatInfo` |
| POST | `/Function` | `FunctionData` | Acceptance result |
| POST | `/Operation` | `OperationData` | Operation result |
| POST | `/Set` | `SetData[]` | boolean |
| GET | `/Get` | None | `GetReturn[]` |
| POST | `/EnterAndExit` | `EnterOrExitData` | `Finish` |

Paths are case-sensitive. A known path with an incorrect HTTP method MUST return 405. An unknown path MUST return 404. `/FunctionSync` is not a CIAI 2.0 endpoint.

## 5. Result envelope

Except for rejections at the HTTP layer, responses use this envelope:

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": {}
}
```

`code` is machine-readable, `message` is human-readable, and `data` contains endpoint-specific data.

| code | Meaning |
|---|---|
| `message.common.success` | Success or request accepted |
| `message.common.failed` | Business failure |
| `message.common.unauthorized` | Unauthorized |
| `message.common.timeout` | Timeout |
| `message.common.server.error` | Server error |
| `message.common.parameters.missing` | Required parameter missing |

Extension codes SHOULD use a reverse-domain or project namespace to avoid collisions with common codes.

## 6. HTTP status codes

| Status | Meaning |
|---|---|
| 200 | The request format is valid; inspect `Result.code` for the business result |
| 400 | Invalid JSON, missing field, or invalid parameter format |
| 404 | Non-standard path |
| 405 | Standard path with the wrong HTTP method |
| 413 | Request body exceeds the configured limit |
| 429 | Function queue or in-flight HTTP resources are full |
| 500 | Unhandled host exception |

A business failure MUST NOT be disguised as `message.common.success`. If Set returns a bare `false` or a successful envelope containing `false`, the host MUST treat that item as failed.

## 7. Function

`POST /Function` accepts a long-running task:

```json
{
  "functionName": "run",
  "functionParam": { "mode": "normal" },
  "instructionId": "instruction-001",
  "nestId": "robot_exchange",
  "labwareInfo": null
}
```

- `functionName` MUST identify a Function advertised by `/Info`.
- `instructionId` SHOULD be unique within the orchestration system.
- The host MUST promptly acknowledge acceptance and MUST NOT block the HTTP request until the device task completes.
- Repeated requests with the same non-empty `instructionId` MUST be acknowledged idempotently and MUST NOT execute the task again.
- A full queue MUST return 429.
- When callbacks are enabled, the completion result MUST retain the original `instructionId` and `nestId`.
- A successful terminal state MUST be proven by device state, a vendor result code, or an explicit completion event. Idle, Ready, Edit, connection success, or a void return alone MUST NOT prove success.

Completion data:

```json
{
  "completion": "finish",
  "errorMsg": null,
  "instructionId": "instruction-001",
  "nestId": "robot_exchange",
  "resultOutput": [
    { "name": "temperatureCelsius", "resultData": 37.0 }
  ]
}
```

`completion` MUST support at least `finish` and `error`. An error completion SHOULD include `errorMsg`.

## 8. Operation, Set, Get, and EnterAndExit

- Operation is a synchronous control action; `operationName` MUST be advertised by `/Info`.
- Set accepts an array. A driver MUST explicitly reject unknown names, invalid types, and values rejected by the device.
- Get returns all public state. A caller interprets `getValue` using the type and unit published by `/Info`.
- CIAI 2.0 publishes one EnterAndExit entry point. `enterOrExitName` dispatches the concrete load, unload, or transfer action.

## 9. Info and declarative capabilities

`/Info` MUST publish:

- device name, model, manufacturer, driver version, and device class;
- Functions, Operations, Gets, and Sets;
- Nests;
- EnterAndExit.

Public names MUST be unique within each category. Invalid method signatures, duplicate names, and malformed form JSON MUST fail during startup, not on the first request.

## 10. Nest

A Nest represents a device location, channel, bay, or storage position. An implementation MUST NOT infer a location role from its name. The device document, device discovery, or deployment configuration MUST supply:

- external robot accessibility;
- source or destination role;
- internal storage, transition, or external exchange role;
- accepted labware, coordinates, posture, column/layer, and height.

Annotations may declare fixed positions. Configuration or device discovery may publish variable positions dynamically.

## 11. Heartbeat

Heartbeat MUST reflect actual availability, including:

- not initialized;
- disconnected;
- device abnormality;
- exhaustion or contention of a critical resource;
- normal availability.

A DLL/API driver without an SDK communication object MUST still derive the correct heartbeat from the vendor session and initialization state. `heartBeatTime` MUST be an RFC 3339 timestamp with a UTC offset.

## 12. Concurrency model

These dimensions are independent:

1. `FunctionalResources`: concurrent Function business resources.
2. `Parallelizability`: business parallelism advertised upstream.
3. `deviceCallResources`: vendor DLL/API call resources.
4. `connections.*.maxConcurrency`: underlying connection resources.

TCP, serial, and similar single-channel request/response protocols MUST be fixed at one transaction and MUST prevent another endpoint from interleaving between a request and its response. HTTP, process, DLL, or API concurrency may exceed one only when the vendor explicitly supports independent sessions.

## 13. Extensions

- Device-specific configuration belongs in `device.settings` and SHOULD be converted into a typed driver configuration.
- Drivers define connection names. The SDK reads every entry under `connections`, but it does not infer a connection from the device name.
- A transport other than TCP, HTTP, Serial, process, or DLL SHOULD use a communication provider without changing the standard endpoints.
- Cancellation, progress, and proactive events are current SDK extensions; they do not form an eighth CIAI 2.0 HTTP endpoint.

## 14. Conformance

An implementation MUST pass language-independent wire tests and the applicable SDK contract tests. Evidence includes:

- seven endpoints, HTTP methods, and JSON fields;
- 400, 404, 405, 413, 429, and 500 behavior;
- Function acceptance, idempotency, queueing, and callbacks;
- registration, heartbeat, Nest, and result semantics;
- exact TCP/serial bytes, frame boundaries, and transaction concurrency;
- configuration diagnostics, startup failure, and graceful shutdown.

See [conformance.md](conformance.md) for the full checklist. OpenAPI and JSON Schema are machine-readable aids. If they conflict with this document, the joint observable behavior of this document and the contract tests takes precedence, and the documentation difference should be reported as an Issue.

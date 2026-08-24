# CIAI

[English](README.en.md) | 简体中文

[![CI](https://github.com/infraSynbio/CIAI/actions/workflows/ci.yml/badge.svg)](https://github.com/infraSynbio/CIAI/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Protocol](https://img.shields.io/badge/CIAI-2.0--preview-orange.svg)](docs/protocol.md)

CIAI（Comprehensive Interface for Autonomous Integration）是一套面向生物实验室自动化设备的开放驱动协议和 SDK。它让调度系统用统一的 7 个 HTTP 接口发现并控制不同厂商的仪器，同时把 TCP、串口、HTTP、现代 DLL/API 和老式 DLL/COM 的接入差异留在驱动内部。

> 当前版本是 `2.0.0-beta.1`。协议和 SDK 可以用于集成验证，但在发布首个稳定版前仍可能进行兼容性调整。

English summary: CIAI is an open protocol and SDK for integrating laboratory automation devices. The repository provides aligned .NET and Java implementations, declarative YAML configuration, contract tests, and vendor-neutral examples.

## 为什么使用 CIAI

- 只有 7 个固定端点：`/Info`、`/HeartBeat`、`/Function`、`/Operation`、`/Set`、`/Get`、`/EnterAndExit`。
- C# 与 Java 使用相同的线上 JSON、错误语义和异步 Function 回调模型。
- 普通 TCP、串口和 HTTP 驱动不需要手写通信工厂、连接代码或信号量。
- YAML 管理端口、证书、连接、超时、并发资源和厂商设置；代码主要保留注解和设备协议实现。
- 串口/TCP 请求–响应强制单通道事务；DLL/API、业务能力与 Function 资源分别管理。
- 支持多连接、共享资源组、动态 Nest、强类型配置、取消、进度、事件和文件工作流。
- 支持老式 x86/.NET Framework/COM SDK 的进程隔离，避免直接加载到现代宿主。
- Function 有界排队、`instructionId` 幂等、HTTP 429、请求体限制和优雅停机均由宿主管理。

## 仓库结构

```text
CiaiControllerSDK/                 .NET 6/.NET 8 SDK
CiaiControllerSDKForJava/          Java 8+ SDK
CiaiControllerSDK.LegacyAdapter/   老 DLL/COM 进程桥接协议
CiaiControllerSDK.ContractTests/   .NET 契约测试
examples/                           无厂商依赖的可运行示例
docs/                               协议、API、安全和符合性文档
schemas/                            JSON Schema
openapi/                            OpenAPI 3.1 描述
```

本仓库不包含厂商 DLL、真实设备驱动、证书私钥、访问令牌或内部调试产物。

## 五分钟运行

### .NET

需要 .NET 8 SDK：

```powershell
dotnet run --project examples/csharp-temperature/Ciai.Example.Temperature.csproj
```

然后访问：

```text
http://127.0.0.1:18080/Info
```

### Java

需要 JDK 8+ 和 Maven 3.8+：

```bash
./mvnw -pl examples/java-temperature -am install
./mvnw -f examples/java-temperature/pom.xml exec:java
```

然后访问：

```text
http://127.0.0.1:18081/Info
```

两个示例表达相同的设备能力，便于比较跨语言实现。

## 最小驱动

通信和宿主初始化来自 `application.yml`，驱动不需要构造函数：

```csharp
[DeviceDriver("温控设备", FunctionalResources = 1)]
public sealed class TemperatureDriver : DeviceDriverBase
{
    [DeviceFunction("run", TitleCN = "运行", TitleEN = "Run")]
    public async Task<Result<Finish>> Run(FunctionData data)
    {
        var parameter = RequireFunctionParam<RunParameter>(data);
        ExecutionCancellationToken.ThrowIfCancellationRequested();
        ReportProgress(10, "设备已接受任务");
        return Result<Finish>.Success(Finish.Success());
    }
}
```

```java
@DeviceDriver(name = "温控设备", functionalResources = 1)
public final class TemperatureDriver extends DeviceDriverBase {
    @DeviceFunction(name = "run", titleCN = "运行", titleEN = "Run")
    public Result<Finish> run(FunctionData data) {
        RunParameter parameter = requireFunctionParam(data, RunParameter.class);
        getCurrentExecution().throwIfCancellationRequested();
        reportProgress(10, "设备已接受任务", null);
        return Result.success(Finish.success());
    }
}
```

完整代码见 [examples](examples/README.md)。

## 声明式配置

默认使用 HTTP，适合本机开发和受控内网验证：

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

HTTPS/mTLS 的完整注释模板位于两套 SDK 的 `application.sample.yml`。证书密码使用 `${ENVIRONMENT_VARIABLE}`，证书和密码不得提交到 Git。生产部署应根据组织安全策略启用 HTTPS 或 mTLS。

## 四种独立资源

| 配置/属性 | 作用 | 是否控制串口/TCP锁 |
|---|---|---|
| `FunctionalResources` | 同时运行的 Function 数量 | 否 |
| `Parallelizability` | 上位系统看到的业务并行能力 | 否 |
| `deviceCallResources` | DLL/API 的真实调用资源 | 否 |
| `connections.*.maxConcurrency` | 每条底层连接资源 | 是 |

TCP、串口和 Modbus 类请求–响应连接始终强制为 1。HTTP、process、DLL/API 只有在厂商明确支持多实例或多会话时才能增加。

## 协议端点

| 方法 | 路径 | 行为 |
|---|---|---|
| GET | `/Info` | 注册信息、能力、Nest 和表单描述 |
| GET | `/HeartBeat` | 驱动和设备健康状态 |
| POST | `/Function` | 接受长任务，完成后可回调 |
| POST | `/Operation` | 同步操作 |
| POST | `/Set` | 批量设置参数 |
| GET | `/Get` | 获取全部公开状态 |
| POST | `/EnterAndExit` | 装载、卸载或转移入口 |

没有 `/FunctionSync`。取消、进度和主动事件目前属于 SDK 宿主扩展，不新增第八个公开端点。

## 文档

- [CIAI 2.0 线协议](docs/protocol.md)
- [API 参考](docs/api-reference.md)
- [配置与通信](docs/configuration.md)
- [安全策略](SECURITY.md)
- [符合性测试](docs/conformance.md)
- [.NET SDK 指南](CiaiControllerSDK/README.md)
- [Java SDK 指南](CiaiControllerSDKForJava/README.md)
- [跨语言一致性](CiaiControllerSDK/PARITY.md)
- [老 DLL/COM 适配器](CiaiControllerSDK.LegacyAdapter/README.md)

## 构建与测试

```powershell
dotnet build CiaiControllerSDK/CiaiControllerSDK.csproj -c Release
dotnet run --project CiaiControllerSDK.ContractTests/CiaiControllerSDK.ContractTests.csproj -c Release
dotnet build examples/csharp-temperature/Ciai.Example.Temperature.csproj -c Release
```

```bash
./mvnw --batch-mode --no-transfer-progress verify
```

契约测试不需要真实设备。厂商 SDK、证书和硬件必须在各驱动项目中单独验证，不能用模拟测试替代。

## 参与贡献

提交代码前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全问题请按 [SECURITY.md](SECURITY.md) 私下报告，不要在公开 Issue 中提交证书、令牌、设备地址或厂商二进制文件。

## 许可证

代码和文档采用 [Apache License 2.0](LICENSE)，直接依赖的归属见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。设备厂商 SDK、商标、协议文档和二进制文件仍受各自权利人的许可约束，Apache-2.0 不授予其再分发权。

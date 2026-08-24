# CiaiControllerSDK (.NET)

面向 CIAI2 设备控制器的 C# SDK。协议语义与 `CiaiControllerSDKForJava` 对齐，支持 .NET 6 兼容构建和 .NET 8 新驱动开发。

## 核心契约

SDK 对外提供 7 个固定端点：

| 端点 | HTTP 方法 | 说明 |
|---|---|---|
| `/Info` | GET | 驱动注册信息 |
| `/HeartBeat` | GET | 驱动与设备状态 |
| `/Function` | POST | 异步功能，完成后可回调 |
| `/Operation` | POST | 即时操作 |
| `/Set` | POST | 参数设置 |
| `/Get` | GET | 状态读取 |
| `/EnterAndExit` | POST | 进出板等动作 |

`/FunctionSync` 不是当前正式端点。

## 构建与测试

```powershell
dotnet build CiaiControllerSDK/CiaiControllerSDK.csproj
dotnet run --project CiaiControllerSDK.ContractTests/CiaiControllerSDK.ContractTests.csproj
```

SDK 多目标构建 `net6.0;net8.0`。新驱动优先 `net8.0`；既有 .NET 6 驱动可继续引用同一项目。

## 最小声明式驱动

通信对象、连接、超时和并发限制都从 `application.yml` 自动创建。普通驱动不需要构造函数，也不需要重写初始化：

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text;

[DeviceDriver("温控器", NameEN = "TemperatureController", FunctionalResources = 2,
    Parallelizability = 1)]
public sealed class TemperatureDriver : DeviceDriverBase
{
    [DeviceFunction("heat", TitleCN = "加热", TitleEN = "Heat")]
    public async Task<Result<Finish>> Heat(FunctionData data)
    {
        var param = RequireFunctionParam<HeatParam>(data);
        var response = await SendAndReadUntilAsync(
            Encoding.ASCII.GetBytes($"HEAT {param.Temperature}\\r\\n"), (byte)'\n', 256);
        return response != null
            ? Result<Finish>.Success(Finish.Success())
            : Result<Finish>.Failed("设备无响应");
    }
}

public sealed class HeatParam
{
    [Required]
    public double? Temperature { get; set; }
}

await DriverHost.RunAsync<TemperatureDriver>("application.yml");
```

驱动作者通常只写三类内容：`DeviceDriver` 设备信息、Function/Operation/Set/Get/EnterExit/Nest 注解，以及注解方法内的协议逻辑。

## 配置

复制 `application.sample.yml` 为驱动的 `application.yml`。`communicationType` 支持 `TCP`、`HTTP`、`Serial`、`DLL`。设备专用配置放在 `device.settings`：

```yaml
device:
  deviceId: "device-001"
  communicationType: "DLL"
  settings:
    simulated: false
```

驱动通过 `Configuration.GetExtraSetting("simulated", false)` 读取自定义值。为兼容旧驱动，`device` 下未知的直接字段也会进入 `ExtraSettings`。未知通信类型会立即报错，不再静默回退为 TCP。

配置支持 `${NAME}` 环境变量和 `${NAME:-default}` 默认值，证书密码和 Token 不需要写进仓库。启动时会校验通信字段、资源数、注解方法签名、同类名称唯一性和 `FormJson` 格式。

串口标准配置包括 `port`、`baudRate`、`dataBits`、`stopBits`、`parity`、`encoding` 及独立读写超时。TCP 也支持独立连接、读取、写入超时。`SerialCommunication`/`TcpCommunication` 会将每个请求-响应作为一个固定单通道事务，多个接口同时调用时不会交叉收发；驱动无需再加串口/TCP信号量。带结束符或定长协议直接使用基类的 `SendAndReadUntilAsync` / `SendAndReadExactAsync`，两种通信共享同一个帧接口。

HTTP 通信的 Connect 表示客户端配置已就绪，不会在初始化时擅自 GET 设备根路径。实际 GET/POST 失败仍反映在每次调用结果中。

## 一个设备有多个连接

只用一个 TCP、串口或 HTTP 时继续使用上面的 `communicationType`，代码最少。只有设备确实同时使用多条链路时才填写 `connections`：

```yaml
device:
  deviceId: "multi-001"
  connections:
    control:
      type: "tcp"
      default: true
      host: "192.168.1.20"
      port: 5000
    balance:
      type: "serial"
      serialPort: "COM3"
      baudRate: 9600
      parity: "even"
      flowControl: "rtscts"
      discardInputBeforeWrite: true
    vendorApi:
      type: "http"
      baseUrl: "https://192.168.1.20/api"
      maxConcurrency: 4
      headers:
        Authorization: "Bearer ${VENDOR_TOKEN}"
      retryCount: 2
      retryDelayMs: 200
```

`default: true` 的连接仍可使用基类原有通信辅助方法。非默认连接这样调用，SDK自动取得该连接的资源锁和重试策略：

```csharp
var reply = await ExecuteConnectionCallAsync("balance", async communication =>
    await ((IFramedCommunication)communication).SendAndReadUntilAsync(
        command, new byte[] { 0x0D, 0x0A }, 4096, ExecutionCancellationToken));
```

字段规则：

- `TCP`、`Serial`、`Modbus` 是单请求-响应通道，`maxConcurrency` 无论填写多少都按 1。
- `HTTP`、`process` 和用户注册的 DLL/API Provider 才读取 `maxConcurrency`，默认 1。
- 多个连接填写相同 `resourceGroup` 时共享一个信号量，适合两个逻辑接口实际共用同一厂商 SDK 或总线；同组必须填写相同并发数。
- `required: false` 允许辅助连接启动失败而主驱动继续运行；默认 `true`。所有已连接项在失败和停机时都会按逆序释放。
- `connectOnStart: false` 只创建而不主动连接，供设备需要延迟登录时使用。

## 厂商自定义协议 Provider

SDK不会试图内置所有 OPC UA、BLE、Modbus 变体或厂商 SDK。实现并注册 `ICommunicationProvider` 后，YAML 的 `type` 就能直接选择它：

```csharp
public sealed class VendorProvider : ICommunicationProvider
{
    public IEnumerable<string> Types => new[] { "vendor-opcua" };
    public void Validate(ConnectionConfiguration c) { /* 缺字段时抛出带说明的异常 */ }
    public ICommunication Create(ConnectionConfiguration c) => new VendorCommunication(c.Settings);
}

CommunicationProviderRegistry.Register(new VendorProvider());
await DriverHost.RunAsync<MyDriver>("application.yml");
```

厂商字段放在该连接的 `settings` 或 `device.settings`。SDK只校验公共字段，不拒绝未知厂商字段，也不要求每个厂商使用同一配置结构。

## 老 DLL、COM 与 x86 驱动

只有与宿主位数、运行时和线程模型完全兼容的现代 DLL 才建议进程内调用。下列任一情况使用 `type: process`：仅 x86、.NET Framework 4.x、COM/STA、需要消息泵、原生依赖冲突或稳定性未知。

```yaml
device:
  connections:
    vendor:
      type: "process"
      default: true
      executable: "./vendor-adapter/VendorAdapter.exe"
      arguments: ["--device", "A1"]
      architecture: "x86"     # 文档说明；真正位数由exe编译目标决定
      framework: "net472"
      apartmentState: "STA"
      maxConcurrency: 1
      shutdownTimeoutMs: 5000
```

适配器可执行程序引用 `CiaiControllerSDK.LegacyAdapter`，按厂商要求编译为 `net472/x86` 并在入口使用 `[STAThread]`。`LegacyAdapterServer.Run(...)` 已实现长度前缀协议；stdout只能传协议帧，日志写stderr。C#和Java宿主使用同一协议。详见 `CiaiControllerSDK.LegacyAdapter/README.md`。

## 厂商强类型配置与启动诊断

```yaml
device:
  settings:
    protocol:
      station: 1
      checksum: "crc16"
```

```csharp
public sealed class ProtocolOptions { public int Station { get; set; } public string Checksum { get; set; } }
var options = Configuration.GetRequiredExtraSetting<ProtocolOptions>("protocol");
```

`GetRequiredExtraSetting` 缺失或类型不匹配会指出 `device.settings.protocol`；可选值使用 `GetExtraSetting` 并给默认值。支持点路径，例如 `GetExtraSetting("protocol.station", 1)`。`ConfigurationValidator.Validate(...)` 可在工具或测试中列出所有公共配置错误和警告，正式初始化会阻止错误配置启动。

## 位置、长任务和文件型设备

固定位置使用 `[DeviceNest]`。型号决定数量、设备启动后发现格口或拓扑来自厂商 API 时，重写 `GetDynamicEquipmentNests()`。SDK只负责发布最终列表，不推断位置含义：机械臂交互位、内部存储位、过渡位、来源/目的地、坐标和姿态必须由驱动作者依据文档填写。

```csharp
protected override IEnumerable<EquipmentNest> GetDynamicEquipmentNests()
{
    foreach (var item in Configuration.GetRequiredExtraSetting<List<NestOptions>>("nests"))
        yield return new EquipmentNest {
            NestName = item.Name,
            NestAccessibility = item.RobotAccessible ? 1 : 0,
            NestIsDestination = item.IsDestination ? 1 : 0,
            TransitionNest = item.TransitionNest
        };
}
```

长 Function 内使用 `ExecutionCancellationToken.ThrowIfCancellationRequested()`，用 `ReportProgress(0..100, message)` 上报进度；宿主可按 `instructionId` 调用 `CancelInstruction`，`EventPublished` 同时承载进度、告警和设备主动事件。这是驱动内部/宿主扩展能力，不会增加第八个公开端点。

文件导入/导出型设备使用 `FileWorkflow`：它把路径限制在配置的根目录内，提供文件大小稳定检测和临时文件原子替换，避免读取半写文件或目录穿越。

## 四种互不混用的并发概念

- `FunctionalResources`：上位系统可同时执行多少个 Function。
- `Parallelizability`：设备业务能力描述，例如是否支持多个培养任务；仅用于注册与调度语义。
- `device.deviceCallResources`：DLL/COM/厂商 API 的底层调用资源数，默认 1。只在真实 API 调用外使用 `ExecuteDeviceCallAsync(...)`，不要包住整个长时间 Function。
- `device.connections.<name>.maxConcurrency`：每条命名连接的底层资源数；TCP/串口固定为 1，HTTP/process/自定义 Provider 按厂商能力配置。

串口和 TCP 的传输通道固定为 1，不读取 `deviceCallResources`。HTTP 保持可并发。DLL/API 若厂商明确支持多实例或多通道，可在 YAML 中把 `deviceCallResources` 调大：

```yaml
device:
  communicationType: "DLL"
  deviceCallResources: 2
  deviceCallTimeoutMs: 30000
```

```csharp
var value = await ExecuteDeviceCallAsync(() => vendorApi.ReadAsync());
```

## 驱动返回值

Function、Operation、Set 和 Enter/Exit 注解方法支持同步、`Task<T>`、`ValueTask<T>`，并支持裸值或 `Result<T>`。使用 `Result<T>` 时业务错误码会原样保留。

Function 的 HTTP 请求会先返回接受结果，再在后台执行。仅当 `callback.enabled: true` 且 URL 有效时发送 Finish 回调；回调会补全请求中的 `instructionId` 与 `nestId`。

Function 使用有界队列；相同非空 `instructionId` 会幂等确认而不会重复执行，队列已满返回 429。服务器还限制在途请求、请求体和停机等待：

```yaml
server:
  maxConcurrentRequests: 100
  maxRequestBodyBytes: 1048576
  functionQueueCapacity: 100
  idempotencyCapacity: 10000
  shutdownTimeoutMs: 30000
```

## HTTPS 说明

.NET 版本使用 Windows `HttpListener`/HTTP.sys。HTTPS 启动会把 PFX/P12 证书绑定到端口，因此需要相应管理员权限或预先配置 URL ACL/SSL 绑定。信任库、叶证书指纹和签发者指纹会参与客户端证书校验。端口已绑定不同证书时，SDK 默认报错且不会删除现有绑定；只有明确设置 `allowReplaceCertificateBinding: true` 才允许替换。

`protocol`、`enabledProtocols` 和 `ciphers` 会保留在配置对象中，但 HTTP.sys 的实际 TLS 策略由 Windows 系统配置控制，不能像 Java `SSLServerSocket` 一样由当前监听器逐项强制。部署时应检查服务器的 Schannel/HTTP.sys 策略。

详细的 Java/C# 差异与有意保留项见 [PARITY.md](PARITY.md)。

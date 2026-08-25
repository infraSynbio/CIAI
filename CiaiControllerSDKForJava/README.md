# CiaiControllerSDK for Java

用于开发 CIAI2 设备驱动的 Java 8+ SDK。它与 .NET SDK 共享相同的 7 个端点、JSON 模型、资源语义和通信行为。

## 最短开发路径

驱动作者通常只需掌握设备文档中的命令、参数、返回值和错误码。通信创建、连接、TCP/串口事务锁、HTTP 服务、Function 排队和 Finish 回调由 SDK 处理。

1. 复制 `src/main/resources/application.sample.yml` 为 `application.yml`。
2. 填写设备信息和通信参数。
3. 继承 `DeviceDriverBase`，添加注解方法。
4. 用 `DriverCli.run(MyDriver.class, args)` 启动。

```java
@DeviceDriver(name = "温控器", functionalResources = 2, parallelizability = 1)
public final class TemperatureDriver extends DeviceDriverBase {

    @DeviceFunction(
        name = "heat",
        titleCN = "加热",
        titleEN = "Heat",
        formJson = "{\"type\":\"object\",\"required\":[\"temperature\"]}")
    public Result<Finish> heat(FunctionData data) {
        HeatParam param = requireFunctionParam(data, HeatParam.class);
        byte[] response = sendAndReadUntil(
                ("HEAT " + param.temperature + "\\r\\n").getBytes(StandardCharsets.US_ASCII),
                (byte) '\n',
                256);
        return response == null
                ? Result.failed("设备无响应")
                : Result.success(Finish.success());
    }

    @DeviceSet(name = "mode", titleCN = "模式")
    public boolean setMode(String mode) {
        return sendAndReadUntil(
                ("MODE " + mode + "\\r\\n").getBytes(StandardCharsets.US_ASCII),
                (byte) '\n',
                256) != null;
    }

    @DeviceGet(name = "state", titleCN = "状态")
    public String getState() {
        byte[] response = sendAndReadUntil(
                "STATE?\\r\\n".getBytes(StandardCharsets.US_ASCII),
                (byte) '\n',
                256);
        return response == null ? "unknown" : new String(response, StandardCharsets.US_ASCII).trim();
    }

    public static final class HeatParam {
        public double temperature;
    }
}
```

```java
public static void main(String[] args) {
    DriverCli.run(TemperatureDriver.class, args);
}
```

直接运行使用 `application.yml`；也支持位置参数、`--config path/to/application.yml`
以及不创建驱动、不连接硬件、不占用端口的
`--validate --config path/to/application.yml`。

默认配置按“当前工作目录 → 驱动 JAR 所在目录 → classpath”解析。公共 adapter、
工作目录、证书和信任库相对路径以配置文件目录为基准；厂商文件路径使用
`getConfiguration().resolvePath(...)`，不要依赖 `user.dir`。

普通 TCP、Serial、HTTP 驱动不需要构造函数、通信工厂、连接代码或信号量。只有厂商 DLL/API 登录、事件订阅、特殊握手等生命周期逻辑才重写初始化与断开。

## 配置优先

```yaml
server:
  port: 8080
  host: "0.0.0.0"
  useHttps: false
  maxConcurrentRequests: 100
  maxRequestBodyBytes: 1048576
  functionQueueCapacity: 100
  idempotencyCapacity: 10000
  shutdownTimeoutMs: 30000

callback:
  enabled: false
  url: ""
  timeoutMs: 30000

device:
  deviceId: "temperature-001"
  communicationType: "Serial"
  deviceCallResources: 1
  deviceCallTimeoutMs: 30000
  serial:
    port: "COM3"
    baudRate: 9600
    dataBits: 8
    stopBits: 1
    parity: "none"
    readTimeoutMs: 5000
    writeTimeoutMs: 5000
    encoding: "utf-8"
  settings:
    simulated: false
    apiToken: "${DEVICE_API_TOKEN}"
```

`${NAME}` 从环境变量读取敏感配置；`${NAME:-default}` 提供默认值。自定义字段使用：

```java
boolean simulated = getConfiguration()
        .getExtraSetting("simulated", Boolean.class, false);
```

`device.settings` 和为兼容旧项目而直接写在 `device` 下的自定义字段都会被映射。配置错误、重复注解名称、错误方法签名和非法 `formJson` 会在创建/启动阶段直接报错。

## 多连接配置

单连接继续用 `communicationType`。设备同时使用 TCP、串口、HTTP 或厂商 API 时改用 `connections`；非空时它优先：

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

```java
byte[] reply = executeConnectionCall("balance", communication ->
        ((IFramedCommunication) communication)
                .sendAndReadUntil(command, new byte[]{13, 10}, 4096));
```

- TCP、Serial、Modbus 的 `maxConcurrency` 固定按 1，业务并行信息不会突破物理通道。
- HTTP、process、DLL/API Provider 的 `maxConcurrency` 默认 1，可按厂商能力调整。
- 相同 `resourceGroup` 的连接共享同一个并发预算，同组并发数必须一致。
- `required` 默认 true；`connectOnStart` 默认 true；失败连接和停机按逆序释放。

## 自定义 Provider 与厂商配置

OPC UA、BLE、Modbus 变体和厂商专用 SDK 通过 `ICommunicationProvider` 注册，不需要修改SDK工厂：

```java
public final class VendorProvider implements ICommunicationProvider {
    public Collection<String> getTypes() { return Collections.singletonList("vendor-opcua"); }
    public void validate(ConnectionConfiguration c) { /* 检查必需厂商字段 */ }
    public ICommunication create(ConnectionConfiguration c) { return new VendorCommunication(c.getSettings()); }
}

CommunicationProviderRegistry.register(new VendorProvider());
DriverCli.run(MyDriver.class, args);
```

SDK只校验公共连接字段，未知厂商字段保持开放。建议把复杂配置放在 `device.settings` 的一个对象里：

```java
ProtocolOptions options = getConfiguration()
        .getRequiredExtraSetting("protocol", ProtocolOptions.class);
int station = getConfiguration().getExtraSetting("protocol.station", Integer.class, 1);
String mapFile = getConfiguration().resolvePath(
        getConfiguration().getRequiredExtraSetting("protocol.mapFile", String.class));
```

必需配置错误会包含 `device.settings.protocol` 路径。`ConfigurationValidator.validate(...)` 可用于配置体检，正式初始化会阻止公共配置错误启动。

## 老 DLL/COM 进程桥接

Java不能直接装载多数 C# 厂商 DLL。C# 宿主遇到 x86、.NET Framework 4.x、COM/STA、消息泵或原生依赖冲突时也不应强行进程内加载。两端统一使用：

```yaml
device:
  connections:
    vendor:
      type: "process"
      default: true
      executable: "./vendor-adapter/VendorAdapter.exe"
      arguments: ["--device", "A1"]
      architecture: "x86"
      framework: "net472"
      apartmentState: "STA"
      maxConcurrency: 1
```

适配器引用 `CiaiControllerSDK.LegacyAdapter` 并按厂商要求编译。协议为小端 Int32 长度加原始字节；stdout只传协议，日志写stderr。Java和C# `ProcessCommunication` 完全相同。

## 动态位置、取消进度与文件设备

固定位置用 `@DeviceNest`。数量由型号决定或拓扑来自设备时重写 `getDynamicEquipmentNests()`。这只解决“列表如何产生”，不推断位置语义；机械臂交互位、内部存储位、过渡位、来源/目的地、坐标和姿态仍由驱动作者依据设备文档填写。

```java
@Override
protected Collection<EquipmentNest> getDynamicEquipmentNests() {
    return vendorTopology.stream().map(this::toEquipmentNest).collect(Collectors.toList());
}
```

长任务通过 `getCurrentExecution().throwIfCancellationRequested()` 响应取消，使用 `reportProgress(0..100, message, data)` 上报进度；宿主按 `instructionId` 调用 `cancelInstruction`，事件监听器还可接收报警和主动上报。它们不增加新的公开HTTP端点。

文件导入/导出型设备使用 `FileWorkflow`，获得根目录路径隔离、稳定文件检测和原子写入，避免半写文件和目录穿越。

## 四种独立的资源概念

| 配置/能力 | 含义 | 是否控制串口/TCP |
|---|---|---|
| `functionalResources` | 可并行执行的 Function 数量 | 否 |
| `parallelizability` | 上位系统看到的设备业务并行能力 | 否 |
| `deviceCallResources` | DLL/COM/厂商 API 的真实调用资源，默认 1 | 否 |
| `connections.<name>.maxConcurrency` | 每条底层连接的资源数 | TCP/Serial固定为1 |

DLL/API 只把真实厂商调用包进资源：

```java
double temperature = executeDeviceCall(() -> vendorApi.readTemperature());
```

不要把整个长时间 Function 包进去。HTTP 设备通信保持可并发。

## 帧读取

TCP 和 Serial 实现同一个帧接口，驱动无需判断具体通信类型：

```java
byte[] fixed = sendAndReadExact(command, 12);
byte[] line = sendAndReadUntil(command, (byte) '\n', 4096);
byte[] crlf = sendAndReadUntil(command, new byte[]{13, 10}, 4096);
```

方法会在同一个事务锁内完成发送和读取，能处理拆包并防止多个接口把响应读串。二进制协议使用 `byte[]`，不要依赖平台换行符。

## 注解与返回

固定注解为 `DeviceDriver`、`DeviceFunction`、`DeviceOperation`、`DeviceSet`、`DeviceGet`、`DeviceNest`、`DeviceEnterExit`。除外部唯一 `name` 外，标题和设备补充信息均可逐步填写；交付前仍应补全用户可见标题、表单和说明。

Function、Operation、Set、Enter/Exit 支持裸值、`Result<T>` 和 `CompletableFuture<T>`。SDK 会等待异步结果并保留业务失败。Set 返回 `false` 或 `Result.success(false)` 会被视为失败。

Function 先返回“已接受”，再通过有界队列执行。相同非空 `instructionId` 会被幂等确认而不会重复执行；队列已满返回 HTTP 429。仅在 `callback.enabled: true` 且 URL 有效时发送回调。

## 固定端点

| 端点 | 方法 |
|---|---|
| `/Info` | GET |
| `/HeartBeat` | GET |
| `/Function` | POST |
| `/Operation` | POST |
| `/Set` | POST |
| `/Get` | GET |
| `/EnterAndExit` | POST |

没有 `/FunctionSync`。

## 日志后端

SDK 只依赖 `slf4j-api`，不会向使用方强行传递 Logback，也不会默认创建 `logs/driver.log`。应用按自己的采集体系选择 `slf4j-simple`、Logback、Log4j2 或其他兼容后端；可运行示例使用 `slf4j-simple`。生产日志不得包含证书密码、令牌、真实样本信息或不必要的原始报文。

## 构建与测试

在仓库根目录运行：

```bash
./mvnw --batch-mode --no-transfer-progress verify
```

契约测试覆盖 DLL 心跳、异步反射返回、Set 失败语义、自定义配置、TCP 原子事务/拆包、HTTP 字节通信和 JSON 空值策略，不依赖真实设备。硬件协议仍需用假设备录制报文或真实设备验证。

HTTPS 使用 Java `SSLContext`。默认协议基线为 Java 8 可用的 TLS 1.2；升级 TLS 1.3 时必须同时修改 `protocol` 和 `enabledProtocols`，并在全部部署 JVM 上验证。证书密码应从环境变量注入，不要提交真实密码、Token 或私钥。

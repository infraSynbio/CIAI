# CIAI2 驱动契约与映射规则

## 目录

1. [固定 HTTP 契约](#固定-http-契约)
2. [注解与返回签名](#注解与返回签名)
3. [旧架构映射](#旧架构映射)
4. [通信语义](#通信语义)
5. [配置映射](#配置映射)
6. [并发、回调与生命周期](#并发回调与生命周期)
7. [序列化兼容](#序列化兼容)
8. [位置与扩展能力](#位置与扩展能力)

## 固定 HTTP 契约

| 端点 | 方法 | 行为 |
|---|---|---|
| `/Info` | GET | 返回 `RegisterInfo`，包含 Function/Operation/Get/Set/Nest/EnterExit |
| `/HeartBeat` | GET | 返回当前驱动、通信、设备或资源状态 |
| `/Function` | POST | 快速接受任务，后台执行并按需回调 Finish |
| `/Operation` | POST | 同步返回操作的 `Result<bool>` |
| `/Set` | POST | 设置一个或多个属性 |
| `/Get` | GET | 汇总全部 Get 方法结果 |
| `/EnterAndExit` | POST | 执行进板/出板等动作 |

已知端点使用错误 HTTP 方法应返回 405；未知路径返回 404；参数错误返回 400；未处理异常返回 500。线上 JSON 继续使用 `code`、`message`、`data` 字段，不把异常堆栈暴露给调用方。

## 注解与返回签名

共有 7 类注解：

| 注解 | 成员形态 | 支持返回值 |
|---|---|---|
| `DeviceFunction` | 方法 `(FunctionData)` | `Finish` 或 `Result<Finish>` |
| `DeviceOperation` | 方法 `(OperationData)` | `bool` 或 `Result<bool>` |
| `DeviceGet` | 无参方法 | 任意可序列化值 |
| `DeviceSet` | 单参数方法 | `bool`、`Result<bool>`，或兼容的无返回实现 |
| `DeviceNest` | 属性 | `EquipmentNest` |
| `DeviceEnterExit` | 方法 `(EnterOrExitData)` | `Finish` 或 `Result<Finish>` |
| `DeviceDriver` | 类 | 驱动元数据与资源能力 |

以上方法可同步返回，也可使用 `Task<T>` / `ValueTask<T>`。升级旧驱动时不要为了迎合某一种语言写法丢弃 `Result<T>` 的业务错误。

`Function` 的完成结果必须带回请求的 `InstructionId` 和 `NestId`。即使驱动方法未主动填写，框架回调也应补全。`EnterExit` 的 Finish 同样保留对应上下文。

## 旧架构映射

| OriginController | SDK |
|---|---|
| OWIN Controller 中按字符串分发 | 每个功能一个注解方法 |
| `Function.json` / `Operation.json` | 注解元数据和 `FormJson` 常量 |
| `Nest.json` | `DeviceNest` 属性与 `DeviceEnterExit` 方法 |
| `Property.json` | `DeviceGet` / `DeviceSet` |
| 手工 HTTP Finish 通知 | SDK 根据 callback 配置统一回调 |
| Unity/Castle AOP | `DeviceDriverBase` 调度、日志、资源控制 |
| Controller 直接调用厂商 DLL | wrapper 接口 + 真实/模拟实现 |
| `Global.json` | `DeviceDriver` 元数据 + `application.yml` |

迁移清单以外部可观察行为为单位。一个旧 Controller 路由中可能分发多个功能，必须拆开并逐项核对；不能只按文件数量判断覆盖率。

## 通信语义

### DLL/COM

- YAML 使用 `communicationType: "DLL"`。
- 驱动可在没有 `ICommunication` 的情况下初始化。
- wrapper 负责连接、断开、异常码翻译、位数/平台约束和本机库释放。
- YAML 配置 `deviceCallResources`（默认 1）和 `deviceCallTimeoutMs`；用 SDK 设备调用辅助方法只包住真实厂商 API 调用。
- 不要把未知的通信类型静默当成 TCP。
- x86、.NET Framework 4.x、COM/STA、消息泵或原生依赖冲突使用 `type: process`。同进程装载必须有运行时、位数和厂商支持证据。

### TCP

- 核对字节编码、网络序、长度头、结束符、CRC、粘包/拆包和半包。
- `SendAsync` 与 `ReceiveAsync` 的超时分别定义；断开和 Dispose 不得永久等待锁。
- SDK 传输层固定使用一个事务资源；一次发送及其响应必须原子完成，不读取 `Parallelizability` 或 `deviceCallResources`。
- 设备主动上报时使用单独接收循环，并支持取消与退出。

### HTTP

- 支持设备协议需要的 GET、POST、PUT、DELETE。
- 明确连接状态代表“客户端可用”还是“最近请求成功”。
- HTTP 非成功状态、网络异常和业务 `Result` 失败分别处理。

### Serial

必须从文档或旧配置确认：

- 端口名、波特率、数据位、停止位、校验位。
- 读超时、写超时、字符编码。
- 命令是否需要 CR、LF、CRLF，或必须发送原始字节。
- 响应是结束字节、固定长度、长度头、空闲间隔还是校验帧。
- 打开连接前后是否需要清空输入缓冲区。
- XON/XOFF、RTS/CTS、DTR 和 RTS 的要求。

文本命令也不要默认使用 `WriteLine`；只有协议明确要求当前平台换行符时才允许。二进制协议始终基于字节数组实现。

串口传输层同样固定使用一个事务资源。多个 Function/Operation/Get 可以并发进入业务代码，但实际串口请求必须排队，且响应只能交给对应请求。

## 配置映射

标准 YAML：

```yaml
device:
  deviceId: "device-id"
  communicationType: "Serial" # DLL / TCP / HTTP / Serial
  deviceCallResources: 1
  deviceCallTimeoutMs: 30000
  serial:
    port: "COM3"
    baudRate: 9600
    dataBits: 8
    stopBits: 1
    parity: "none"
    timeoutMs: 5000
    readTimeoutMs: 5000
    writeTimeoutMs: 5000
    encoding: "utf-8"
  settings:
    simulated: false
```

`device` 下无法识别的直接字段和 `device.settings` 都进入 `DeviceConfiguration.ExtraSettings`。驱动使用：

```csharp
var simulated = Configuration.GetExtraSetting("simulated", false);
```

不要再次读取 `application.yml`，否则相对路径、测试注入和宿主传入配置会产生两套状态。

敏感值使用 `${NAME}` 从环境变量注入，允许 `${NAME:-default}` 作为非敏感默认值。缺失且无默认值时阻止启动，不记录解析后的密码或 Token。

多连接使用：

```yaml
device:
  connections:
    control:
      type: tcp
      default: true
      host: 192.168.1.20
      port: 5000
    balance:
      type: serial
      serialPort: COM3
      baudRate: 9600
      resourceGroup: device-bus
    vendor:
      type: process
      executable: ./adapter/VendorAdapter.exe
      maxConcurrency: 1
```

TCP/Serial/Modbus 的有效并发恒为 1。HTTP/process/厂商 Provider 才读取 `maxConcurrency`。相同 `resourceGroup` 共用预算且配置值必须一致。

证书配置需区分服务端证书、信任根和客户端证书要求。启用回调开关必须真实生效；禁用时不得创建无效的零超时 `HttpClient`。

## 并发、回调与生命周期

- `FunctionalResources` 表示可并行执行的 Function 数量；资源等待应有有限超时，默认 30 秒。
- `Parallelizability` 是上位系统使用的设备业务能力，例如是否支持多个培养任务；不得据此给整个驱动方法加锁。
- Serial/TCP 固定单通道事务，HTTP 可并发。DLL/API 使用独立的 `deviceCallResources`；Operation、Get、心跳和设备事件不自动占用 Function 资源。
- `connections.maxConcurrency` 限制某个连接；`deviceCallResources` 是旧式进程内 DLL/API 的兼容资源。迁移时选一种真实调用边界，不重复套锁。
- 初始化失败应使宿主启动失败，不应继续对外报告可用。
- `Connect` 时订阅事件，`Disconnect` 时对称取消；重复初始化不得产生重复订阅。
- Function HTTP 请求返回“已接受”后后台执行。仅在 callback.enabled 为 true 时 POST Finish。
- 回调失败要记录但不能让后台任务成为未观察异常。
- Function 进入有界队列；相同非空 `InstructionId` 幂等确认而不重复执行；队列满返回 429。HTTP 入口同时限制在途请求和请求体，停机等待在途请求与 Function 到配置超时。

心跳建议映射：

| 条件 | 状态 |
|---|---|
| 未初始化 | `Monitoring` 或驱动异常 |
| SDK 通信对象已断开 | `EquipmentAbnormal` |
| Function 资源全部占用 | `Monitoring` |
| 设备报告故障 | 对应设备异常/错误状态 |
| 初始化完成且设备健康 | `Normal` |

## 序列化兼容

OriginController 的 `JavaScriptSerializer` 比 `System.Text.Json` 宽松。表单把数字作为字符串提交，而模型使用 `int`/`double` 时，按字段添加：

```csharp
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public int Mode { get; set; }
```

同时检查：字段大小写、枚举表示、缺失值、`null`、时间格式、布尔字符串和扩展 YAML 数值类型。不要通过全局吞错掩盖单个模型定义错误。

## 位置与扩展能力

- 静态位置使用注解；数量/拓扑由配置或设备决定时使用动态 Nest 方法。
- 动态生成不等于自动推断。对每个 Nest 从文档或用户输入填写机械臂可达性、存储/过渡角色、来源/目的地、labware、坐标和姿态。
- 上位系统协议已有字段优先；协议无法表达的厂商内部语义保留在驱动自定义配置/Provider，不擅自增加上位系统不认识的注册 JSON。
- 用户可以为 OPC UA、BLE、Modbus 变体或厂商 API 实现 `ICommunicationProvider`；SDK公共配置验证只检查公共字段，未知厂商 settings 保持开放。
- 长任务按 `instructionId` 支持取消和进度；公开 HTTP 契约仍只有固定 7 个端点。

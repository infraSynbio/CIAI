# Configuration

CIAI 使用 `application.yml` 同时配置驱动 HTTP 服务、Function 回调和设备通信。正常使用者只编辑 YAML；驱动开发者只在新增厂商字段时定义强类型 Options 并使用这些值。

## 自动读取边界

SDK 自动读取：

- `server`：HTTP/HTTPS、端口、请求上限、Function 队列和停机；
- `callback`：完成回调开关、URL 和超时；
- `device.communicationType` 与单连接 `tcp/http/serial`；
- `device.connections` 下的所有命名连接；
- `device.settings` 下的厂商配置。

SDK不会根据 `[DeviceDriver]` 的设备名称猜测连接或设置名称。命名连接会进入字典，驱动通过固定键选择：

```yaml
device:
  connections:
    command:
      type: tcp
      default: true
    event:
      type: tcp
```

```csharp
await ExecuteConnectionCallAsync("event", communication => ReadEvent(communication));
```

```java
executeConnectionCall("event", communication -> readEvent(communication));
```

只有一条连接时优先使用简短的 `communicationType + tcp/http/serial` 配置。确有多条物理通道时才使用 `connections`。

## 厂商强类型配置

```yaml
device:
  settings:
    vendor:
      station: 1
      model: "A200"
      simulated: false
```

C#：

```csharp
var options = Configuration.GetRequiredExtraSetting<VendorOptions>("vendor");
```

Java：

```java
VendorOptions options = getConfiguration()
    .getRequiredExtraSetting("vendor", VendorOptions.class);
```

已有字段不需要用户写读取代码。新增字段需要驱动开发者在 Options 类中增加属性并在协议/API实现中使用；仅把未知字段写进 YAML 不会自动改变设备行为。

## 通信选择

| 类型 | 使用场景 | 并发 |
|---|---|---|
| TCP | 原始 TCP 请求–响应 | 固定 1 |
| Serial | 串口请求–响应 | 固定 1 |
| HTTP | 厂商 HTTP API | 按厂商能力 |
| DLL | 兼容的进程内 DLL/API | 默认 1 |
| process | x86/.NET Framework/COM/STA 老 SDK | 默认 1 |
| custom provider | OPC UA、BLE、Modbus 变体、gRPC 等 | Provider定义 |

串口必须明确端口、波特率、数据位、停止位、校验、流控、DTR/RTS、读写超时、编码、缓冲清理和帧结束方式。二进制、定长或校验帧不得使用按行文本接口。

## HTTP 与 HTTPS

样例默认：

```yaml
server:
  useHttps: false
```

启用 HTTPS：

1. 将 `useHttps` 改为 `true`；
2. 取消 `certificate`、`ssl` 注释；
3. 将证书放在部署目录，不提交 Git；
4. 通过环境变量设置密码；
5. 需要双向认证时再启用 `trustStore` 和 `clientAuth`。

注释中的 `${VARIABLE}` 不会在 HTTP 模式解析；真正取消注释后，如果必填环境变量缺失，启动必须失败并给出变量名称。

TLS 默认基线为 `TLSv1.2`，因此 Java 8、Java 11 和 .NET 部署可以使用同一份基础配置。只有确认全部目标 JVM、Windows/Schannel 策略和对端都支持时，才把 `protocol` 与 `enabledProtocols` 同时改为 `TLSv1.3`。`ciphers` 留空时使用运行平台的安全策略；确需固定套件时，应先在每个目标运行环境完成握手测试。

## 密钥和路径

- 密码、Token、客户端密钥和真实证书不得提交。
- 示例中的相对路径相对于配置文件所在目录解析。
- .NET 使用 `DriverHost.ValidateConfiguration(...)`，Java 使用
  `DriverHost.validateConfiguration(...)` 或 `DriverCli --validate` 做无硬件预检；
  预检不得创建驱动、连接设备或占用监听端口。
- 厂商自定义文件路径使用 .NET `Configuration.ResolvePath(...)` 或 Java
  `getConfiguration().resolvePath(...)`，不要二次读取 YAML，也不要依赖当前工作目录。
- process 适配器、文件工作流和下载目录必须限制在部署者明确允许的根目录。
- 不得把厂商二进制复制到本仓库；由使用者根据厂商许可在部署阶段提供。

完整字段模板：

- [.NET application.sample.yml](../CiaiControllerSDK/application.sample.yml)
- [Java application.sample.yml](../CiaiControllerSDKForJava/src/main/resources/application.sample.yml)
- [application.yml JSON Schema](../schemas/application.schema.json)

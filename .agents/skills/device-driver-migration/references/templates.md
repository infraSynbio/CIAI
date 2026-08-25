# C# 驱动模板

## 目录

1. [项目文件](#项目文件)
2. [启动入口](#启动入口)
3. [配置](#配置)
4. [驱动骨架](#驱动骨架)
5. [DLL/API 特殊生命周期](#dllapi-特殊生命周期)
6. [方法示例](#方法示例)
7. [多连接与自定义 Provider](#多连接与自定义-provider)
8. [旧 DLL 进程适配器](#旧-dll-进程适配器)
9. [动态位置与长任务](#动态位置与长任务)

占位符必须用真实设备信息替换。只复制目标设备需要的部分，不要保留无用依赖或虚构功能。

## 项目文件

新驱动优先使用 .NET 8；只有厂商 DLL 或部署环境要求时才选择 .NET 6。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\CiaiControllerSDK\CiaiControllerSDK.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="application.yml" CopyToOutputDirectory="PreserveNewest" />
    <None Update="server.controller" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

厂商 DLL 才增加 `<Reference>` 和 `HintPath`。日志、JSON、YAML 包如果 SDK 的传递依赖已满足，不要重复固定版本；驱动直接使用了某包的公开 API 时再显式引用。

## 启动入口

使用配置文件重载，它会一次加载并注入服务器与设备配置。默认从EXE所在目录读取，
因此双击、Windows服务和命令行切换工作目录时行为一致；传入路径时，以配置文件目录
作为adapter、证书和数据文件相对路径的基准：

```csharp
using CiaiControllerSDK.WebServer;

var configPath = DriverHost.ResolveConfigPath(
    args.Length > 0 ? args[0] : "application.yml");
await DriverHost.RunAsync<MyDeviceDriver>(configPath);
```
同步入口可用 `DriverHost.Run<MyDeviceDriver>(configPath)`。默认示例必须使用HTTP且不能
要求证书环境变量；HTTPS/mTLS只放在完整注释块中，生产启用后才读取密码。

安装、CI或首次运行先执行 `DriverHost.ValidateConfiguration(configPath).ThrowIfInvalid()`；
它不创建驱动、不连接设备、不占用监听端口。公共adapter与证书相对路径由SDK按配置
文件目录解析，不修改全局工作目录。

## 配置

```yaml
server:
  port: 12345
  host: "0.0.0.0"
  useHttps: false
  maxConcurrentRequests: 100
  maxRequestBodyBytes: 1048576
  functionQueueCapacity: 100
  idempotencyCapacity: 10000
  shutdownTimeoutMs: 30000

callback:
  url: "http://platform.example/overSeer/finish"
  timeoutMs: 30000
  enabled: true

device:
  deviceId: "my-device-001"
  communicationType: "Serial"
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
    flowControl: "none"
    dtrEnable: false
    rtsEnable: false
    discardInputBeforeWrite: false
  settings:
    simulated: false
```

正式部署启用 HTTPS/mTLS 时，从仓库 SDK 示例复制完整证书结构并替换证书与密码。
共同基线使用TLS 1.2且默认不固定密码套件；TLS 1.3必须在目标Java/.NET、操作系统和
对端全部确认支持后启用。端口不能靠静态清单猜测：扫描仓库所有 YAML，再检查本机占用。

## 驱动骨架

```csharp
[DeviceDriver(
    name: "设备中文名",
    NameEN = "DeviceName",
    Model = "Model",
    Manufacturer = "Vendor",
    Version = "1.0.0",
    FunctionalResources = 1,
    CanEmergencyStop = true)]
public sealed class MyDeviceDriver : DeviceDriverBase
{
    // 只添加注解方法。YAML通信创建、连接、超时和TCP/串口事务锁由SDK完成。
}
```

普通 TCP/Serial/HTTP 驱动不要添加构造函数、`SetCommunication` 或通信工厂。只有设备协议要求登录、握手、事件订阅等额外步骤时才重写初始化和断开，并先/后正确调用基类。

## DLL/API 特殊生命周期

```csharp
private readonly IDeviceWrapper _device = new VendorDeviceWrapper();

public override async Task<bool> InitializeAsync()
{
    if (!await _device.ConnectAsync())
        return false;
    return await base.InitializeAsync();
}

public override async Task DisconnectAsync()
{
    try
    {
        await _device.DisconnectAsync();
    }
    finally
    {
        await base.DisconnectAsync();
    }
}
```

DLL/API 驱动把真实厂商调用包进独立设备资源，不要包整个长任务：

```csharp
var state = await ExecuteDeviceCallAsync(() => _device.ReadStateAsync());
```

资源数只在 YAML 的 `deviceCallResources` 配置；默认 1。`FunctionalResources` 和 `Parallelizability` 不替代该资源。

## 方法示例

```csharp
[DeviceFunction(name: "run", TitleCN = "运行", TitleEN = "Run")]
public async Task<Result<Finish>> RunAsync(FunctionData data)
{
    var param = RequireFunctionParam<RunParam>(data);
    var ok = await ExecuteDeviceCallAsync(() => _device.RunAsync(param));
    if (!ok)
        return Result<Finish>.Failed("设备拒绝运行");

    return Result<Finish>.Success(new Finish
    {
        Completion = "finish",
        InstructionId = data.InstructionId,
        NestId = data.NestId
    });
}
```

参数模型使用普通语言类型表达设备文档中的字段，并用 `Required`、范围、枚举等校验标记；不要在每个方法里重复解析字典/JSON：

```csharp
public sealed class RunParam
{
    [Required]
    public string Mode { get; set; }

    [Range(1, 100)]
    public int Speed { get; set; }
}

[DeviceOperation(name: "reset", TitleCN = "复位", TitleEN = "Reset")]
public Task<Result<bool>> ResetAsync(OperationData data) => _device.ResetAsync();

[DeviceGet(name: "state", TitleCN = "状态", Type = "string")]
public string GetState() => _device.State;

[DeviceSet(name: "mode", TitleCN = "模式", Type = "string")]
public Result<bool> SetMode(string mode) => _device.SetMode(mode);

[DeviceEnterExit(name: "plate_in", TitleCN = "进板", TitleEN = "Plate In")]
public Task<Result<Finish>> PlateInAsync(EnterOrExitData data) => _device.PlateInAsync(data);
```

模型名与命名空间冲突时用明确别名；不要为所有项目机械复制一组未使用的别名。

## 多连接与自定义 Provider

只有设备确实有多条连接时使用：

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
    api:
      type: vendor-api
      maxConcurrency: 2
      settings:
        endpointId: A
```

```csharp
var reply = await ExecuteConnectionCallAsync("balance", async communication =>
    await ((IFramedCommunication)communication)
        .SendAndReadUntilAsync(command, new byte[] { 0x0D, 0x0A }, 4096,
            ExecutionCancellationToken));
```

自定义通信实现 `ICommunicationProvider`，在 `DriverHost.Run` 前注册。Provider 的 `Validate` 只验证自己真正需要的厂商字段；不要要求所有厂商共享一份自定义 schema。

## 旧 DLL 进程适配器

宿主配置：

```yaml
device:
  connections:
    vendor:
      type: process
      default: true
      executable: ./adapter/VendorAdapter.exe
      architecture: x86
      framework: net472
      apartmentState: STA
      maxConcurrency: 1
```

适配器项目按厂商约束设置 `TargetFramework`/`PlatformTarget`，引用 `CiaiControllerSDK.LegacyAdapter`：

```csharp
[STAThread]
static void Main()
{
    using var vendor = new VendorSdkWrapper();
    LegacyAdapterServer.Run(request => vendor.Execute(request));
}
```

请求内容由驱动和适配器约定（推荐带 operation、arguments、requestId 的 JSON/二进制 DTO），外层 framing 固定为小端 Int32 长度 + payload。stdout 禁止日志。

## 动态位置与长任务

```csharp
protected override IEnumerable<EquipmentNest> GetDynamicEquipmentNests()
{
    foreach (var item in _topology)
        yield return new EquipmentNest
        {
            NestName = item.Name,
            NestAccessibility = item.RobotAccessible ? 1 : 0,
            NestIsDestination = item.IsDestination ? 1 : 0,
            TransitionNest = item.TransitionNest,
            NestCoordinate = item.Coordinate
        };
}

[DeviceFunction("long_run")]
public async Task<Result<Finish>> LongRun(FunctionData data)
{
    for (var progress = 0; progress <= 100; progress += 5)
    {
        ExecutionCancellationToken.ThrowIfCancellationRequested();
        await ExecuteConnectionCallAsync("control", c => SendStepAsync(c),
            ExecutionCancellationToken);
        ReportProgress(progress, "设备执行中");
    }
    return Result<Finish>.Success(Finish.Success());
}
```

位置角色必须来自设备文档或用户配置。动态方法只减少重复代码，不能自动判断存储位是否可供机械臂交互。

# Java 声明式驱动模板

## 启动入口

Java 8+ 应用只需要选择配置文件并启动；日志后端由应用选择，SDK只提供SLF4J门面：

```java
public final class Main {
    public static void main(String[] args) {
        DriverCli.run(MyDeviceDriver.class, args);
    }
}
```

交付的可执行JAR必须支持 `--validate --config application.yml`，并从任意工作目录做一次
无硬件预检。厂商文件路径必须调用 `getConfiguration().resolvePath(...)`，禁止读取
`user.dir` 后手工拼接。

安装程序或CI可先运行：

```java
ConfigurationValidationReport report = DriverHost.validateConfiguration(
        MyDeviceDriver.class, "application.yml");
report.throwIfInvalid();
```

## 最小驱动

```java
@DeviceDriver(
    name = "设备中文名",
    nameEN = "DeviceName",
    model = "Model",
    manufacturer = "Vendor",
    version = "1.0.0",
    functionalResources = 1)
public final class MyDeviceDriver extends DeviceDriverBase {

    @DeviceFunction(name = "run", titleCN = "运行", titleEN = "Run")
    public Result<Finish> run(FunctionData data) {
        RunParam param = requireFunctionParam(data, RunParam.class);
        return executeDeviceCall(() -> vendor.run(param))
                ? Result.success(Finish.success())
                : Result.failed("Device rejected run");
    }

    @DeviceGet(name = "state", titleCN = "状态", type = "string")
    public String state() { return currentState; }
}
```

普通TCP/HTTP/Serial驱动不写构造函数、连接工厂和传输锁。只有登录、握手、事件订阅或厂商DLL生命周期才重写初始化/断开，并保持对称清理。

## 配置

Java与C#使用相同公共YAML字段。默认HTTP、关闭回调；设备差异放 `device.settings`：

```yaml
server:
  port: 8080
  host: "0.0.0.0"
  useHttps: false
callback:
  enabled: false
device:
  deviceId: "device-001"
  communicationType: "Serial"
  serial:
    port: "COM3"
    baudRate: 9600
    dataBits: 8
    stopBits: 1
    parity: "none"
    encoding: "utf-8"
    flowControl: "none"
    readTimeoutMs: 5000
    writeTimeoutMs: 5000
  settings:
    simulated: true
```

厂商强类型配置使用 `getRequiredExtraSetting("vendor", VendorOptions.class)`；可选字段才使用带默认值的 `getExtraSetting`。多连接、process、动态Nest与状态机规则分别按主Skill和其他references执行，不复制C#语法。

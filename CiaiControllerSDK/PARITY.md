# Java 与 .NET SDK 一致性说明

以实际源码和 `CiaiControllerSDK.ContractTests` 为准，README 仅作说明。

| 能力 | Java | .NET | 当前结论 |
|---|---|---|---|
| 固定端点 | 7 个 | 7 个 | 一致；无 `/FunctionSync` |
| 注册信息 | 六类设备成员 | 六类设备成员 | .NET 已补 Enter/Exit 注册 |
| Function 返回 | 裸值/`Result` + CompletableFuture | 裸值/`Result` + Task/ValueTask | 异步结果均等待并保留失败 |
| Operation/Set 返回 | 裸值/`Result` + CompletableFuture | 裸值/`Result` + Task/ValueTask | Set 的 false 均视为失败 |
| Function 资源 | 30 秒获取超时 | 30 秒获取超时 | 一致 |
| 声明式初始化 | YAML自动创建通信 | YAML自动创建通信 | 一致；驱动无需构造函数/手写连接 |
| DLL/COM 驱动 | 显式 `DLL` + 可配置设备调用资源 | 显式 `DLL` + 可配置设备调用资源 | 一致 |
| TCP/Serial并发 | 固定单通道事务锁 | 固定单通道事务信号量 | 一致；发送与响应不可交叉 |
| 业务并行与通信并行 | 相互独立 | 相互独立 | `Parallelizability`不控制通信锁 |
| 心跳 | 根据初始化、连接和资源推导 | 根据初始化、连接和资源推导 | 一致；设备专用错误由驱动覆盖 |
| 心跳时间 | RFC 3339 带时区偏移 | RFC 3339 带时区偏移 | 一致；可被标准 `date-time` 解析器直接读取 |
| 回调开关 | 生效 | 生效 | .NET 已补 disabled 安全路径 |
| Finish 上下文 | 补 instruction/nest | 补 instruction/nest | 一致 |
| HTTP 设备通信 | GET/POST/PUT/DELETE + 字节接口 | GET/POST/PUT/DELETE + 字节接口 | Connect 均只表示客户端就绪，不探测根路径 |
| Serial 参数 | 构造器和YAML支持data/stop/parity/encoding/读写超时 | 同左 | 一致 |
| TCP/Serial 帧读取 | 公共定长/结束字节接口 | 公共定长/结束字节接口 | 发送及响应原子化 |
| 自定义配置 | typed ExtraSettings + 兼容直接字段 | typed ExtraSettings + 兼容直接字段 | 一致 |
| 环境变量 | `${NAME}` / 默认值 | `${NAME}` / 默认值 | 一致 |
| Function 入口保护 | 有界队列 + instructionId 幂等 | 有界队列 + instructionId 幂等 | 队列满均返回 429 |
| HTTP 服务保护 | 有界线程/请求体/优雅停机 | 在途信号量/请求体/优雅停机 | 可观察行为一致 |
| HTTPS 信任库 | Java SSLContext | Windows 证书链/自定义根 | 证书信任语义一致，平台实现不同 |
| TLS 协议/套件 | 监听器可逐项设置 | 由 HTTP.sys/Schannel 控制 | 平台限制；部署配置必须单独核验 |
| 服务初始化失败 | 启动失败 | 启动失败 | 一致 |
| 已知端点错误方法 | 405 | 405 | 一致 |
| 命名多连接/资源组 | 支持 | 支持 | TCP/Serial固定1；HTTP/DLL/API可配置 |
| Provider扩展 | `ICommunicationProvider` | `ICommunicationProvider` | 自定义type无需修改SDK工厂 |
| 老DLL进程桥接 | 长度前缀process | 长度前缀process | 共用C# LegacyAdapter协议 |
| 串口流控 | XON/XOFF、RTS/CTS、DTR/RTS | 同左 | YAML字段一致 |
| 多字节帧尾 | 支持 | 支持 | 收发保持同一事务锁 |
| 动态Nest | `getDynamicEquipmentNests` | `GetDynamicEquipmentNests` | 语义由驱动作者填写 |
| 取消/进度/事件 | instruction上下文+监听器 | instruction上下文+事件 | 不增加公开HTTP端点 |
| 配置诊断 | 路径化错误 | 路径化错误 | 厂商未知字段保持开放 |
| 文件工作流 | 路径隔离/稳定/原子写 | 同左 | 文件型设备通用辅助 |

## 共同增强

- 同时支持 .NET 6 和 .NET 8，避免升级 SDK 破坏已有驱动。
- 注解方法支持语言原生异步返回，反射异常会解包为真实设备错误。
- 同类别注解名称重复、签名错误或 `FormJson` 非法时启动即报错。
- 两端未知通信类型都立即报错，避免拼写错误导致连接错误地址。
- `clientAuth.enabled: false` 优先于 mode，确保关闭配置真实生效。
- Java 注解除外部 `name` 外均提供默认值，最小驱动与 C# 一样只需声明核心信息。

## 有意保留的平台差异

- .NET 同时支持 .NET 6 与 .NET 8；Java 目标字节码为 Java 8。
- Java HTTPS 可通过 `SSLContext` 设置协议/套件；.NET `HttpListener` 的最终 TLS 策略由 HTTP.sys/Schannel 控制。
- .NET 自动证书绑定发现端口已有不同证书时默认失败，不进行破坏性删除；Java 直接从 KeyStore 创建监听器，不修改系统端口绑定。

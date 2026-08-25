---
name: device-driver-migration
description: 将设备文档、厂商 SDK、旧版 OriginController 或已有 CIAI2 驱动转换、开发、审计或升级为 CiaiControllerSDK/CiaiControllerSDKForJava 驱动。用户提到“开发驱动”“转换/迁移驱动”“按 SDK 模式”“CIAI2Controller”“CiaiControllerSDK”“拿文档做驱动”“优化老驱动”“对齐 Java/C# SDK”“检查串口/TCP/HTTP/DLL 通信”或要求比较新旧驱动功能时使用。
---

# CIAI2 设备驱动开发与迁移

## 基准与目标

把 `CiaiControllerSDKForJava` 视为跨语言协议语义基准，把当前仓库的 `CiaiControllerSDK` 视为 C# 实现基准。语言写法可以不同，但注册信息、7 个 HTTP 端点、结果码、异步回调、资源约束、心跳和设备通信行为必须一致。

固定端点只有：`/Info`、`/HeartBeat`、`/Function`、`/Operation`、`/Set`、`/Get`、`/EnterAndExit`。不要根据 README 或旧代码擅自增加 `/FunctionSync`；只有路由实现和调用契约共同存在时才算正式端点。

默认交付可构建、可启动、可验证的驱动，不以“代码已生成”作为完成条件。

当用户区分“内部开发仓库”和“对外开源仓库”时，把内部仓库作为当前事实来源；除非
用户再次明确授权发布，否则不得读取、同步、提交或推送开源仓库。发布应作为独立步骤，
先检查内部差异、敏感信息、厂商二进制许可证和生成物，再选择性同步。

## 不可跳过的确定性流程

除非用户只要求解释概念，否则严格按以下顺序执行。不要边浏览一个文件边直接改代码。

1. **锁定范围**：记录来源目录、目标目录、目标语言、是否允许改SDK、是否允许操作发布仓库；检查 dirty worktree，保留用户改动。
2. **只读盘点**：运行 `scripts/inventory_driver.py <source>`，再用 `rg` 核实入口、项目、配置、二进制和通信命中。脚本结果只是导航，不是行为证据。
3. **分类证据**：完整读取 [references/intake-and-decisions.md](references/intake-and-decisions.md)，按 `confirmed/inferred/unknown` 标记关键事实，建立能力、配置、通信、状态四张表。
4. **做决策门**：明确单/多连接、进程内/sidecar、帧策略、资源预算、Nest来源和长任务成功证据。任一安全关键项为 `unknown` 时保守实现并列待确认，不能猜。
5. **读取目标契约**：检查实际SDK源码、`spec/ciai-driver-api.openapi.yaml`、`spec/application.schema.json` 和契约测试。源码与测试优先于旧README。
6. **先写测试夹具**：把已确认的报文字节、状态序列、配置错误和旧行为写成测试，再改驱动。普通代码生成不得取代协议测试。
7. **最小实现**：配置负责连接/超时/资源，注解负责上位能力，代码只实现厂商协议与必要生命周期。不得顺便重写无关厂商逻辑。
8. **分层验收**：配置预检 → SDK构建 → 驱动构建 → 无硬件契约/假设备 → 模拟服务7端点 → 真实硬件。完整执行 [references/state-and-testing.md](references/state-and-testing.md) 的适用矩阵。
9. **差异交付**：列出完成项、行为差异、命令与结果、`unknown`、硬件未验证项。能力表没有未解释缺口才能称为完成。

## 先选择任务模式

1. **文档新开发**：读取设备协议、厂商 SDK/API 文档、示例代码、DLL 依赖和部署要求，建立命令与字段清单，再实现驱动。
2. **旧架构迁移**：读取 OriginController 的 Controller、配置、模型、通信和回调代码，逐项映射到 SDK。
3. **已有驱动升级**：先做源驱动与目标驱动差异审计，只补缺失或错误语义；不要无依据重写已工作的厂商逻辑。
4. **SDK 跨语言对齐**：以实际源代码和契约测试为准，比较 Java/C# 的公开模型、注解、配置、通信能力与运行时行为。

## 必须读取的内容

- 总是读取目标 SDK 的 `Core/DeviceDriverBase`、`Attributes/`、`Models/`、`Config/`、`Communication/`、`WebServer/`。
- Java/C# 对齐时，同时读取两边对应实现，不只比较 README。
- 旧驱动迁移时，读取原项目全部相关配置与通信实现；先用 `rg` 建立文件和入口清单。
- 从文档开发时，读取用户提供的全部相关页面；表格、图片或扫描页也要检查，不能只抽取正文。
- 所有任务先读取 [references/intake-and-decisions.md](references/intake-and-decisions.md)。
- 需要精确字段和返回签名时读取 [references/mapping-rules.md](references/mapping-rules.md)。
- 创建 C# 项目或配置文件时读取 [references/templates.md](references/templates.md)。
- 创建 Java 项目或配置文件时读取 [references/java-templates.md](references/java-templates.md)。
- 涉及长任务、状态、取消、进程隔离或进入验证阶段时读取 [references/state-and-testing.md](references/state-and-testing.md)。
- 审计、升级或交付前读取 [references/parity-checklist.md](references/parity-checklist.md)。

## 执行流程

### 1. 建立可追溯清单

在改代码前记录：

- 每个 Function、Operation、Get、Set、Nest、Enter/Exit 的外部名称、标题、参数、返回值和错误行为。
- 初始化、连接、断开、急停、复位、事件订阅和状态查询。
- 通信类型、报文格式、编码、校验、超时、重试、帧边界、并发限制。
- 是否存在多条物理连接、共享总线/厂商会话、主动事件通道、文件交换目录或外部进程。
- 配置字段、默认值、证书、回调地址、部署平台和厂商二进制依赖。

已有驱动升级必须输出“来源 → 目标 → 状态（已覆盖/缺失/行为不同）”映射，再实施修改。

### 2. 选择通信实现与隔离边界

- **单连接**：继续使用 `communicationType` 和 `tcp/http/serial`，这是普通驱动的最短路径。
- **多连接**：使用 `device.connections` 命名每条连接。默认连接可沿用基类辅助方法；其他连接使用 `ExecuteConnectionCallAsync`/`executeConnectionCall`。同一底层总线或厂商会话填写同一 `resourceGroup`。
- **现代 DLL/厂商 SDK（进程内）**：只有宿主运行时、位数、原生依赖和线程模型完全兼容时才使用 `communicationType: "DLL"` + wrapper。真实 API 调用包进 `ExecuteDeviceCallAsync`/`executeDeviceCall`。
- **旧 DLL/COM（进程隔离）**：x86、.NET Framework 4.x、COM/STA、消息泵、原生依赖冲突或稳定性未知时，使用 `connections.<name>.type: process` 和 `CiaiControllerSDK.LegacyAdapter`。适配器按厂商要求编译，stdout 只传长度前缀协议，日志写 stderr。不要尝试把 net472/x86 DLL 直接载入 net8/x64 宿主。
- **TCP**：默认由 YAML 和基类自动创建 `TcpCommunication`。确认连接生命周期、粘包/拆包、编码、超时和重连。
- **HTTP**：使用 `HttpCommunication`；按设备协议实现任意方法、Header/认证和路径，区分 HTTP 失败与业务失败。初始化只表示客户端配置就绪，不用 GET 根路径猜测健康。
- **Serial**：必须明确端口、波特率、数据位、停止位、校验、流控、DTR/RTS、读写超时、编码、缓冲清理和帧结束条件。二进制/定长/校验帧不得用 `WriteLine`。
- **其他协议**：Modbus 变体、OPC UA、BLE、SOAP/WCF、gRPC/SiLA 或厂商传输实现 `ICommunicationProvider` 并注册自定义 `type`。不要给普通驱动复制一份 SDK 工厂。
- **文件型设备**：使用 `FileWorkflow` 限制根路径，等待文件稳定并原子写入；仍需从文档确认命名、编码、轮询、归档和失败重试。

厂商进程适配器的“启动任务”请求必须尽快返回任务标识或状态快照，不能在一次
stdin/stdout 请求里阻塞到设备任务结束。否则同一个适配器无法接收暂停、恢复、
终止、心跳等控制命令。长任务由宿主短事务轮询，或由独立事件通道上报。
还要定义 sidecar 崩溃后的恢复语义：状态查询可重连并重新配置；启动、加样、开门
等非幂等命令不得盲目自动重放。恢复后只有设备仍能提供本次任务的明确成功证据时
才可报成功，否则保守失败并提示人工核对现场状态。

不要在普通驱动中手写通信工厂、连接或串口/TCP锁。串口和TCP由SDK固定为单通道请求-响应事务；`FunctionalResources`只约束Function，`Parallelizability`只描述业务能力，二者都不控制传输锁。HTTP可并发。DLL/API的调用资源是第三个独立维度，默认1，只有厂商明确支持多实例/多通道时才调大。

`connections.maxConcurrency` 是每条底层连接的资源数：TCP/Serial/Modbus 始终强制 1；HTTP/process/DLL/API 按厂商能力配置。它与上面的业务字段保持独立。

### 3. 实现 SDK 契约

- 驱动类使用 7 类注解：`DeviceDriver`、`DeviceFunction`、`DeviceOperation`、`DeviceGet`、`DeviceSet`、`DeviceNest`、`DeviceEnterExit`。
- 同一类别内的注解 `name` 必须唯一；注册信息必须完整包含 Enter/Exit。
- Function 保留 `InstructionId`、`NestId` 并由 SDK 异步回调完成结果；不要在驱动中重复 POST Finish。
- C# 方法可返回裸值或 `Result<T>` 的同步、`Task<T>`、`ValueTask<T>` 形式；错误码和错误消息不得被包装成成功。
- 初始化失败必须阻止服务进入可用状态。断开后清理事件、连接和本地状态。
- 审计旧驱动是否绕过基类生命周期：覆盖初始化时必须保留配置校验、连接状态和失败清理；
  `functionalResources` 必须大于0。无依据的 `tryAcquire()` 后无条件 `release()` 会造成许可膨胀，
  双重 `release()` 和静态全局信号量都必须修复或由SDK资源助手替代。
- 心跳必须反映未初始化、通信断开、资源耗尽和设备异常，不能永久返回 Normal。
- 对厂商 API 建立显式状态证据表：可启动状态、运行中状态、暂停状态、成功终态、
  失败终态、取消终态和恢复/空闲状态必须分开。`Idle`、`Ready`、`Edit`、连接成功、
  命令返回 `void` 或“请求已接受”均不得自行推断为任务成功。
- 从 YAML 读取设备自定义字段时使用 `DeviceConfiguration.GetExtraSetting<T>()`，不要在驱动里第二次解析同一配置文件。
- 厂商文件路径使用 `Configuration.ResolvePath(...)` / `getConfiguration().resolvePath(...)`；
  Java可执行入口优先 `DriverCli.run(..., args)`，交付前从任意目录运行 `--validate --config ...`。
- 必填的厂商配置使用 `GetRequiredExtraSetting<T>`/`getRequiredExtraSetting` 并按一个对象转换为驱动自定义类型；可选值才给默认值。SDK公共诊断不得拒绝未知厂商字段。
- Function/Operation/EnterExit 参数优先使用 `RequireFunctionParam<T>` / `requireFunctionParam(..., T.class)` 等强类型助手；参数模型表达字段类型和校验，不在业务方法里强转 Map 或手工处理 JSON 节点。
- TCP/Serial 的定长或结束字节协议使用基类公共 `SendAndReadExact` / `SendAndReadUntil` 帮助方法，发送与对应读取必须处于同一个事务。
- CRLF、特殊尾标等多字节帧使用多字节 delimiter 重载，不退回单次 Receive。
- 固定 Nest 用注解；型号配置或设备发现产生的位置重写动态 Nest 方法。SDK只发布列表，不猜测机械臂交互位、内部存储位、过渡位、来源/目的地、坐标或姿态，这些信息必须来自设备文档或用户明确输入。
- 长 Function 在循环和厂商调用边界检查执行上下文取消状态，并上报进度；主动报警/事件使用统一事件机制，不增加第八个 HTTP 端点。
- 优先生成无构造函数、无通信初始化样板的声明式驱动；配置放在 `application.yml`，代码只保留设备/接口注解和协议或厂商API实现。只有DLL事件订阅、特殊握手等设备生命周期逻辑才重写初始化与断开。

### 4. 配置与首次运行体验

- 示例默认使用 HTTP、关闭回调和安全的模拟模式；HTTPS/mTLS 作为完整注释模板保留。
  注释中的 `${ENVIRONMENT_VARIABLE}` 不得触发启动校验，只有取消注释并启用HTTPS后才要求密码。
- TLS 默认以 TLS 1.2 作为 Java 8、Java 11+、.NET 6/8 的共同基线；密码套件默认交给
  JVM/操作系统安全策略。启用TLS 1.3或固定套件时必须验证目标运行时真实支持，禁止静默降级。
- EXE/JAR 双击或从任意工作目录启动时，应从可执行文件/JAR所在位置解析默认
  `application.yml`；SDK会以该配置文件目录解析公共adapter与证书相对路径。厂商自定义
  `device.settings` 文件路径仍由驱动以配置目录语义显式处理，不依赖偶然的当前工作目录。
- `connections` 下的键只是驱动自定义连接名，例如 `fluent`、`balance`、`control`；
  它不依赖设备名称。代码通过同一键选择连接，README必须明确列出每个键的用途。
- Java SDK只依赖SLF4J门面，不把Logback/Log4j2作为运行时依赖发布；示例应用自行选择日志后端。
- 每个配置示例必须说明“最少必填项、默认值、可选项、生产切换步骤、错误示例”，并保证
  示例文件可以被对应SDK实际解析，而不是只供阅读。
- 安装、CI或首次运行先调用 C# `DriverHost.ValidateConfiguration` / Java
  `DriverHost.validateConfiguration`；该预检不得创建驱动、连接硬件或占用监听端口。

### 5. 保持行为而不是逐行翻译

- 保留外部名称、参数兼容、默认值、单位、Nest 位置、命令顺序、等待条件和错误语义。
- 对每个 Nest 单独确认角色：外部机械臂可达、内部存储、过渡、来源/目的地、labware 类型、坐标和姿态。不以“都是位置”合并语义。
- 将阻塞式轮询改为可取消异步等待；避免在 `async` 方法里使用 `Thread.Sleep`。
- 事件订阅放在连接/初始化阶段，断开时对称取消，防止重复订阅和内存泄漏。
- 状态事件和状态查询可同时存在时，事件负责低延迟、查询负责事件丢失/延迟兜底，
  两者必须进入同一个幂等状态归并函数。终态应绑定当前任务标识或“已启动”上下文，
  防止把连接前遗留的 Finished/Idle 状态算作本次任务结果。
- 厂商封装若通过 Error/Notify 事件报告失败、而方法本身吞掉异常或返回 `void`，每个
  命令都要关联并检查该事件结果；仅包一层 `try/catch` 不构成成功判定。
- 模拟模式实现相同接口与关键状态转换，不能只返回固定成功。
- 不确定的协议字段标记为待确认，不臆造命令、校验算法或成功条件。

### 6. 验证

按风险从小到大执行：

1. 构建目标 SDK 的所有目标框架。
2. 构建目标驱动及受影响的现有驱动。
3. 运行契约测试，至少覆盖 7 类注册、`Result<T>` 返回、异步回调、错误码、资源超时、声明式配置注入、心跳和配置映射。
4. 多连接测试命名选择、默认连接、共享资源组和失败回滚；对串口/TCP 运行假设备或录制报文，验证原始字节、多字节帧边界和并发请求不会交叉。
5. 在模拟模式启动服务，检查 7 个端点的方法、状态码和 JSON 字段。
6. 有真实设备时再做硬件联调；没有硬件时明确哪些项目尚未实测。

状态机驱动的模拟器至少提供：正常成功、明确失败、未出现成功终态却回到空闲、
暂停/恢复、终止/取消五条路径。测试必须证明“回到空闲但缺少成功证据”会失败。
进程隔离还要通过真实子进程做协议测试，不能只直接调用适配器类。

不得因为本机缺少 Java、厂商 DLL、证书或硬件而声称相关验证已完成。说明缺少项，并完成仍可执行的静态、构建和模拟验证。

SDK 或宿主升级还必须覆盖：重复注解名/错误签名/非法 FormJson 的启动失败；Java `CompletableFuture` 和 C# Task/ValueTask 的真实完成结果；DLL 无通信对象心跳；未知或返回 false 的 Set；Function 有界队列、重复 `instructionId`、429；请求体上限和优雅停机。

新增通用能力时还覆盖：自定义 Provider 注册与未知 type 报错、命名连接资源组、TCP/Serial 并发固定 1、配置路径诊断、动态 Nest、取消/进度事件、process 长度前缀兼容和文件路径越界拒绝。

## 交付要求

- 汇报实现结果、关键差异、测试命令与结果、未验证的硬件/环境项。
- 指出任何有意保留的语言差异，但确认其线上 JSON 和设备行为等价。
- 保留用户已有改动；不要覆盖无关的 dirty worktree 文件。
- 新驱动优先 `net8.0`；需要兼容既有 `net6.0` 驱动时保留多目标或明确兼容策略。

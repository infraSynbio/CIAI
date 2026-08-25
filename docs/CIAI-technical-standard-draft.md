# CIAI 技术标准

> **非规范性历史/扩展草案。** 本文包含尚未由当前 SDK 实现的未来安全、治理和平台能力，也包含与公开预览配置不同的强制 mTLS 提案。CIAI 2.0 当前兼容性必须以 [protocol.md](protocol.md)、[OpenAPI](../openapi/ciai-2.0.yaml)、JSON Schema 和契约测试为准。本文不得作为当前实现通过符合性测试的依据。

# 合成生物制造仪器设备自主集成接口与协议

**CIAI: Comprehensive Interface for Autonomous Integration**

---

| 项目 | 内容 |
|------|------|
| 标准编号 | CIAI-STD-2.0 |
| 版本 | 2.1 |
| 状态 | 征求意见稿 |
| 适用范围 | 合成生物制造领域仪器设备的驱动集成、通信协议、互操作规范及相关技术管理要求 |

---

## 前言

本标准由CIAI技术委员会提出并归口。

本标准起草单位：深圳合成生物大设施。

本标准主要起草人：刘荣。

本标准为第2版，替代CIAI-STD-1.0。主要技术变化如下：
- 新增"技术与管理规范"独立分册（第二部分）；
- 新增"专项规范"独立分册（第三部分），涵盖生物安全、伦理、应急预案、系统统一接口、数据共享规则、知识产权六大专项；
- 所有量化指标补充取值依据与设定原因（附录E）；
- 原规范性附录调整为资料性附录，代码示例与配置模板移入附录；
- 章节按分部分、大拆小原则重构，支持单章节独立引用与修订；
- **安全配置分级**：开发/受控内网可使用默认HTTP，生产按风险显式启用HTTPS；TLS 1.2为跨运行时基线，全链路验证后推荐TLS 1.3，高风险控制网络推荐mTLS（NEED模式）；
- 新增完整注解参考（第11.3节）、日志与图标管理规范（第11.5-11.7节）、驱动实例管理（第11.8节）、配置校验规范（第11.9节）；
- 补充错误码定义（notfound/badrequest/forbidden/error）、数据模型字段完善（ResultOutput name/resultData, EquipmentSetInfo setValue）、通信配置参数完善（Serial细粒度参数及独立连接/读/写超时等）；
- 新增YAML配置完整层级结构（附录D.1）。

---

## 引言

合成生物制造是融合生物学、工程学与信息科学的前沿交叉领域。其实验平台通常由数十至上百台异构仪器设备组成，设备来源多样、通信协议各异、数据格式不统一，导致系统集成成本高、扩展性差、互操作困难。

本标准旨在建立一套统一、开放、安全的设备集成接口与技术管理规范，使异构设备能够在统一协议框架下实现即插即用、互联互通与自动化协同调度，为合成生物制造领域的研究与生产提供标准化技术基础。

---

# 第一部分：产品规范

## 1 范围

本部分规定了合成生物制造仪器设备自主集成接口与协议的产品技术要求，包括系统架构、通信协议、接口定义、数据模型、设备描述与注册以及动态表单规范。

本部分适用于：
- 合成生物制造实验平台中的各类仪器设备（温控、移液、孵育、检测、存储、转移等类别）；
- 设备驱动程序的设计、开发与功能验证；
- 平台调度系统与设备之间的通信交互。

---

## 2 规范性引用文件

下列文件对于本标准的应用是必不可少的：

- GB/T 25069—2022 信息安全技术 术语
- IETF RFC 8446: The Transport Layer Security (TLS) Protocol Version 1.3
- IETF RFC 5246: The Transport Layer Security (TLS) Protocol Version 1.2
- IETF RFC 7230: Hypertext Transfer Protocol (HTTP/1.1)
- IETF RFC 8259: The JavaScript Object Notation (JSON) Data Interchange Format
- ISO 8601: Data elements and interchange formats — Information interchange — Representation of dates and times

---

## 3 术语与定义

| 术语 | 英文 | 定义 |
|------|------|------|
| CIAI | Comprehensive Interface for Autonomous Integration | 合成生物制造仪器设备自主集成接口与协议（本标准简称） |
| 驱动 | Driver | 封装设备通信逻辑与业务操作的独立服务程序 |
| 平台 | Platform | 负责设备调度、任务编排、状态监控的中央控制系统 |
| 功能 | Function | 设备可执行的、具有明确输入输出定义的业务操作（如加热、振荡） |
| 操作 | Operation | 对设备的即时控制指令（如停止、复位、自检） |
| 设置 | Set | 对设备参数进行配置的行为 |
| 获取 | Get | 读取设备当前状态或参数值的行为 |
| 进出 | Enter/Exit | 样品或耗材的载入与卸载操作 |
| 位置 | Nest | 设备上可放置耗材的物理位置 |
| 耗材 | Labware | 用于承载样品的标准化器皿（如微孔板） |
| 资源 | Resource | 设备功能并发执行能力的量化表示 |
| 心跳 | HeartBeat | 驱动定期向平台报告自身健康状态的机制 |
| 回调 | Callback | 功能异步执行完成后，驱动主动通知平台的机制 |
| 指令ID | InstructionId | 平台下发的每条功能执行指令的唯一标识 |

---

## 4 系统架构

### 4.1 总体架构模型

CIAI采用**驱动端服务化**架构：每台设备运行一个独立的驱动HTTP/HTTPS服务，平台作为客户端通过RESTful API与各驱动交互。

**架构示意图**：详见附录D。

**架构量化指标**：

| 指标 | 要求值 | 取值依据 |
|------|--------|----------|
| 单驱动最小内存占用 | ≤ 64 MB | 经验值：基于.NET 6 / Java 8最小运行时环境实测，确保嵌入式设备可部署 |
| 单驱动启动时间 | ≤ 10 s | 经验值：覆盖注解扫描、通信初始化、服务端口绑定全流程的95分位耗时 |
| 单平台最大管理驱动数 | ≥ 200 | 工程推算：大型合成生物实验平台预计设备数量上限的2倍冗余 |

### 4.2 驱动内部架构

每个驱动服务由以下核心模块组成（按分层依赖关系排列）：
1. **HTTP/HTTPS 服务层**：暴露7个标准REST端点，负责请求路由与响应封装；
2. **驱动核心层（DeviceDriverBase）**：功能调度、资源信号量管理、注解扫描注册；
3. **通信抽象层（ICommunication）**：屏蔽底层通信协议差异，统一与物理设备交互。

### 4.3 交互时序

标准交互流程遵循"注册 → 心跳 → 指令 → 回调"四阶段模型，详见附录D。

---

## 5 设备驱动模型

### 5.1 驱动类声明

设备驱动必须以驱动注解标记类声明，提供设备元数据。注解属性定义如下：

| 属性 | 类型 | 必填 | 说明 |
|------|------|------|------|
| name | string | 是 | 设备中文名称 |
| nameEN | string | 是 | 设备英文名称 |
| model | string | 是 | 设备型号 |
| manufacturer | string | 是 | 设备制造商 |
| version | string | 否 | 驱动版本号，默认"1.0.0" |
| equipmentType | int | 是 | 设备类型编码（见5.2） |
| functionalResources | int | 否 | 功能并发资源数，默认值1 |
| canEmergencyStop | boolean | 否 | 是否支持急停，默认true |
| runtimeAccessibility | int | 否 | 运行时可访问性（0=不可访问, 1=可访问），默认1 |
| parallelizability | int | 否 | 可并行性（0=不可并行, 1=可并行），默认0 |
| equipmentClass | string | 否 | 设备分类标签 |
| author | string | 否 | 驱动开发者 |
| icon | string | 否 | 设备图标（Base64编码，data URI格式） |
| iconFile | string | 否 | 设备图标文件路径（相对于icon/目录，自动从文件系统或类路径加载） |

**量化指标取值依据**：
- `functionalResources` 默认值1：基于单数设备物理特性——同一时刻仅能执行一项功能。多通道设备（如多通道移液器）可按实际通道数设置 >1。
- `parallelizability` 默认值0：保守策略——默认禁止功能并行执行以防止物理冲突。仅当设备制造商明确声明支持并行操作时设为1。

### 5.2 设备类型分类与编码

| 编码 | 类型 | 说明 | 分类依据 |
|------|------|------|----------|
| 1 | 核心设备 | 执行实验操作的核心仪器 | 直接参与实验流程的关键设备 |
| 2 | 转移设备 | 在设备间转移耗材的机械装置 | 承担物流功能的设备 |
| 3 | 辅助设备 | 提供辅助功能的设备 | 不直接参与核心实验但支持流程运转 |
| 4 | 存储设备 | 存放耗材或样品的设备 | 承担存储功能的设备 |

### 5.3 驱动基类行为规范

驱动基类（DeviceDriverBase）提供6项标准行为：
1. **初始化生命周期**：`Initialize()` → 建立通信连接 → 标记已初始化；
2. **注解扫描注册**：自动扫描子类功能注解并注册路由映射；
3. **资源信号量管理**：根据 `functionalResources` 创建信号量，功能执行前获取、执行后释放；
4. **同步/异步双模**：每个操作同时提供同步和异步方法；
5. **通信抽象**：通过 `ICommunication` 接口与物理设备通信；
6. **参数反序列化**：提供JSON参数转强类型对象的工具方法。

### 5.4 通信接口规范

驱动与物理设备之间的通信通过 `ICommunication` 接口抽象，标准定义以下通信实现：

| 通信类型 | 适用场景 | 必配参数 | 选配参数 |
|---------|---------|---------|---------|
| TCP | Socket通信设备 | Host, Port | ConnectionTimeout, ReadWriteTimeout |
| HTTP | HTTP API设备 | BaseUrl | ConnectionTimeout |
| Serial | 串口设备 | SerialPort, BaudRate | DataBits（默认8）, StopBits（默认1）, Parity（默认None）, ConnectionTimeout |

---

## 6 通信协议规范

### 6.1 传输层协议

| 项目 | 要求 |
|------|------|
| 应用层协议 | HTTP/1.1 或 HTTPS |
| 数据格式 | JSON（UTF-8编码） |
| Content-Type | `application/json; charset=UTF-8` |
| SDK默认端口 | 8080（HTTP，开发/受控内网配置） |
| 连接模式 | 长连接，支持Keep-Alive |

**量化指标取值依据**：
- SDK以8080作为无需证书的首次运行默认值；生产端口由部署网络与安全策略确定，HTTPS通常使用443或受控专用端口。
- UTF-8编码：兼容中英文标识与特殊字符，是JSON标准（RFC 8259）规定的默认编码。

### 6.2 HTTPS/TLS安全要求（生产部署）

| 项目 | 要求 |
|------|------|
| TLS版本 | TLS 1.2为Java 8/.NET 6/8共同基线；全链路验证后推荐TLS 1.3 |
| 加密套件 | 默认使用JVM/操作系统安全策略；固定套件前必须逐一验证目标运行时 |
| 服务端证书 | PKCS12格式（.pfx/.p12），由受信任CA签发 |
| 客户端认证 | 根据部署风险选择 `NONE`、`WANT` 或 `NEED`；高风险控制网络推荐mTLS `NEED` |
| 信任锚 | 支持配置独立TrustStore（PKCS12格式）或复用KeyStore |

**实施依据**：
- TLS 1.2是Java 8、Java 11+与.NET 6/8可共同部署的基线；TLS 1.0/1.1明确排除。TLS 1.3只有在全部目标JVM、操作系统策略、代理和客户端完成真实握手验证后启用。
- mTLS可同时验证平台与驱动身份，但证书生命周期和现场运维成本更高，应由组织风险评估决定；涉及高风险机械动作、样本或生产网络时推荐 `NEED`。
- SDK示例默认HTTP且关闭回调，便于首次运行。生产环境应使用HTTPS/mTLS或等效的受控网络保护；一旦配置HTTPS，证书或环境变量错误必须阻止启动，不能静默降级到HTTP。

### 6.3 请求/响应封装规范

所有接口响应遵循统一JSON封装格式：

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": { }
}
```

- `code`：响应状态码（字符串），取值见附录A；
- `message`：人类可读的响应描述；
- `data`：业务数据载荷，类型随接口变化。

---

## 7 接口定义

### 7.1 接口总览

| 端点 | HTTP方法 | 用途 | 执行模式 | 幂等性 |
|------|---------|------|---------|--------|
| `/Info` | GET | 获取设备注册描述信息 | 同步 | 是 |
| `/HeartBeat` | GET | 获取驱动心跳状态 | 同步 | 是 |
| `/Function` | POST | 执行设备功能 | 异步（回调） | 否 |
| `/Operation` | POST | 执行设备操作 | 同步 | 否 |
| `/Set` | POST | 设置设备参数 | 同步 | 是 |
| `/Get` | GET | 获取设备状态 | 同步 | 是 |
| `/EnterAndExit` | POST | 执行进出操作 | 同步 | 否 |

### 7.2 GET /Info — 设备注册描述

**请求**：无请求体。

**响应数据结构**：

```
RegisterInfo
├── basicInfo                    // 基础信息（同驱动注解属性）
└── advancedInfo                 // 高级信息
    ├── equipmentFunctions[]     // 功能列表
    ├── equipmentOperations[]    // 操作列表
    ├── equipmentSetInfos[]      // 设置参数列表
    ├── equipmentGetInfos[]      // 获取参数列表
    ├── equipmentNests[]         // 位置列表
    └── equipmentEnterAndExit    // 进出操作
```

响应示例见附录D。

### 7.3 GET /HeartBeat — 心跳检测

**请求**：无请求体。

**响应**：

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": {
    "heartBeatStatus": 0,
    "heartBeatTime": "2026-05-19T10:30:00+08:00"
  }
}
```

`heartBeatStatus` 取值见第14节。

### 7.4 POST /Function — 功能执行

**请求体**：

```json
{
  "functionName": "Heat",
  "instructionId": "instr-20260519-001",
  "nestId": "sample_position",
  "equipmentName": "TemperatureController",
  "userId": "user001",
  "taskId": "task-20260519-001",
  "functionParam": {},
  "labwareInfo": {}
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| functionName | string | 是 | 功能名称，须与注册信息中的functionName匹配 |
| instructionId | string | 是 | 平台分配的指令唯一标识 |
| nestId | string | 否 | 目标位置标识 |
| equipmentName | string | 否 | 设备名称 |
| userId | string | 否 | 操作用户标识 |
| taskId | string | 否 | 所属任务标识 |
| functionParam | object | 否 | 功能参数，结构由formJson定义 |
| labwareInfo | object | 否 | 耗材信息 |

**同步响应**（立即返回202 Accepted语义）：

```json
{ "code": "message.common.success", "message": "Function accepted", "data": "Function accepted" }
```

**异步回调**（由驱动POST到平台回调URL，见第15节）。

### 7.5 POST /Operation — 操作执行

**请求体**：

```json
{ "operationName": "Stop", "operationParam": null }
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| operationName | string | 是 | 操作名称 |
| operationParam | object | 否 | 操作参数 |

**响应**：`Result<Boolean>`。

### 7.6 POST /Set — 参数设置

**请求体**（数组，支持批量设置）：

```json
[
  { "setName": "TargetTemperature", "setValue": "37.0" },
  { "setName": "ShakeSpeed", "setValue": "200" }
]
```

批量设置按数组顺序依次执行，任一失败则整体返回失败。

### 7.7 GET /Get — 状态获取

**请求**：无请求体。

**响应**：`Result<List<GetReturn>>`，返回所有已注册获取参数的当前值。

### 7.8 POST /EnterAndExit — 进出操作

**请求体**：

```json
{ "enterOrExitName": "Load", "enterOrExitValue": null }
```

**响应**：`Result<Finish>`。

---

## 8 数据模型定义

### 8.1 统一响应封装 Result\<T\>

| 字段 | 类型 | 说明 |
|------|------|------|
| code | string | 响应状态码（见附录A） |
| message | string | 响应描述信息 |
| data | T（泛型） | 业务数据载荷 |

### 8.2 功能完成回调 Finish

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| completion | string | 是 | `"finish"`（成功）或 `"error"`（失败） |
| errorMsg | string | 否 | 错误信息（completion="error"时必填） |
| instructionId | string | 是 | 回传平台的指令标识 |
| nestId | string | 否 | 位置标识 |
| resultOutput | ResultOutput[] | 否 | 结果输出键值对列表 |

**ResultOutput 结构**：

| 字段 | 类型 | JSON key | 说明 |
|------|------|---------|------|
| name | string | `name` | 结果键名 |
| resultData | object | `resultData` | 结果值 |

### 8.3 心跳信息 HeartBeatInfo

| 字段 | 类型 | 说明 |
|------|------|------|
| heartBeatStatus | int | 心跳状态码（见第14节） |
| heartBeatTime | string | 时间戳（ISO 8601格式，含时区） |

### 8.4 功能数据 FunctionData

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| functionName | string | 是 | 功能名称 |
| instructionId | string | 是 | 指令唯一标识 |
| nestId | string | 否 | 目标位置 |
| equipmentName | string | 否 | 设备名称 |
| userId | string | 否 | 用户标识 |
| taskId | string | 否 | 任务标识 |
| functionParam | object | 否 | 功能参数 |
| labwareInfo | Labware | 否 | 耗材信息 |

### 8.5–8.8 其余数据模型

OperationData、SetData、GetReturn、EnterOrExitData的定义及JSON Schema详见附录B。

---

## 9 设备描述与注册规范

### 9.1 注册信息结构

注册信息通过 `/Info` 端点返回，分为 `basicInfo`（基础信息，来源于驱动注解）和 `advancedInfo`（高级信息，来源于功能/操作/设置/获取/位置/进出注解的扫描注册结果）两部分。

### 9.2 功能描述 EquipmentFunction

| 字段 | 类型 | 说明 |
|------|------|------|
| functionName | string | 功能名称（唯一标识符） |
| functionTitleCN | string | 中文显示标题 |
| functionTitleEN | string | 英文显示标题 |
| functionDescription | string | 功能描述 |
| functionCategoryCN | string | 中文分类 |
| functionCategoryEN | string | 英文分类 |
| functionDefaultPeriod | string | 默认执行时长（秒） |
| functionFormJsonStructure | string | 动态表单JSON（见第10节） |
| iconBlack / iconWhite | string | 图标（Base64） |

### 9.3 操作描述 EquipmentOperation

| 字段 | 类型 | 说明 |
|------|------|------|
| operationName | string | 操作唯一标识 |
| operationTitleCN / operationTitleEN | string | 中/英文标题 |
| operationDescription | string | 操作描述 |
| operationFormJsonStructure | string | 动态表单JSON |

### 9.4 设置描述 EquipmentSetInfo

| 字段 | 类型 | 说明 |
|------|------|------|
| setName | string | 参数名称 |
| setTitleCN / setTitleEN | string | 中/英文标题 |
| setType | string | 输入类型：`input`, `number`, `select` |
| setValue | string[] | 可选值列表（select类型时使用，如`["option1","option2"]`） |
| setUnit | string | 单位（如"°C"、"rpm"、"mL"） |
| setDescription | string | 参数描述 |

### 9.5 获取描述 EquipmentGetInfo

| 字段 | 类型 | 说明 |
|------|------|------|
| getName | string | 参数名称 |
| getTitleCN / getTitleEN | string | 中/英文标题 |
| getType | string | 值类型：`float`, `int`, `boolean`, `string` |
| getUnit | string | 单位 |
| getDescription | string | 参数描述 |

### 9.6 位置描述 EquipmentNest

核心字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| nestName | string | 位置名称 |
| labwareType | string | 兼容耗材类型（逗号分隔） |
| nestAccessibility | int | 可访问性（0/1） |
| nestHeight | float | 位置高度（mm） |
| nestColumnCo | int | 列数 |
| nestLayerCo | int | 层数 |
| nestIsDestination | int | 是否可作为目标位置（0/1） |

完整字段定义及进出表单字段见附录D。

### 9.7 进出操作描述 EquipmentEnterAndExit

| 字段 | 类型 | 说明 |
|------|------|------|
| enterAndExitName | string | 操作名称 |
| enterAndExitTitleCN / enterAndExitTitleEN | string | 中/英文标题 |

---

## 10 动态表单规范

### 10.1 表单结构

动态表单（FormJson）用于描述功能执行时用户需输入的参数结构。驱动通过 `functionFormJsonStructure` 字段声明，平台据此渲染参数输入界面。

推荐格式（字段列表）：

```json
{
  "fields": [
    { "name": "temperature", "type": "number", "label": "目标温度(°C)", "default": 37, "min": -20, "max": 200, "required": true }
  ]
}
```

兼容格式（嵌套结构）详见附录D。

### 10.2 字段类型与属性

| 类型 | 对应控件 | 支持属性 |
|------|---------|---------|
| number | 数字输入框 | min, max, default |
| int | 整数输入框 | min, max, default |
| string | 文本输入框 | default, maxLength |
| boolean | 开关/复选框 | default |
| select | 下拉选择框 | options（含label/value）, default |

---

# 第二部分：技术与管理规范

## 11 驱动开发规范

### 11.1 开发流程

驱动开发遵循10步标准流程：

1. 继承驱动基类 `DeviceDriverBase`；
2. 声明 `@DeviceDriver` 注解；
3-8. 依次实现功能（`@DeviceFunction`）、操作（`@DeviceOperation`）、设置（`@DeviceSet`）、获取（`@DeviceGet`）、进出（`@DeviceEnterExit`）方法，声明位置属性（`@DeviceNest`）；
9. 在构造函数中根据配置创建通信实现；
10. 通过 `DriverHost` 启动HTTP/HTTPS服务。

参考代码示例见附录C。

### 11.2 方法签名规范

| 方法类型 | 同步签名返回类型 | 异步签名返回类型 | 接收参数 |
|---------|----------------|----------------|---------|
| 功能 | `Result<Finish>` | `Task<Finish>` | `FunctionData` |
| 操作 | `Result<Boolean>` | `Task<bool>` | `OperationData` |
| 设置 | `Result<Boolean>` | `Task<bool>` | `string`（设置值） |
| 获取 | `string` / `double` | — | 无 |
| 进出 | `Result<Finish>` | `Task<Finish>` | `EnterOrExitData` |

### 11.3 注解参考

本标准定义7种注解（Annotation/Attribute），用于声明式定义驱动的设备信息和能力。注解在运行时通过反射扫描注册，驱动启动时自动构建路由映射与注册信息。

#### @DeviceDriver — 驱动类声明

**目标**：类（Class） | **保留策略**：运行时（Runtime）

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 设备中文名称 |
| nameEN | string | — | 是 | 设备英文名称 |
| model | string | — | 是 | 设备型号 |
| manufacturer | string | — | 是 | 设备制造商 |
| version | string | "1.0.0" | 否 | 驱动版本号 |
| equipmentType | int | 1 | 是 | 设备类型编码（1=核心, 2=转移, 3=辅助, 4=存储） |
| functionalResources | int | 1 | 否 | 功能并发资源数 |
| canEmergencyStop | boolean | true | 否 | 是否支持急停 |
| runtimeAccessibility | int | 1 | 否 | 运行时可访问性（0=不可, 1=可） |
| parallelizability | int | 0 | 否 | 可并行性（0=不可, 1=可） |
| equipmentClass | string | "" | 否 | 设备分类标签 |
| author | string | "" | 否 | 驱动开发者 |
| icon | string | "" | 否 | 设备图标（Base64 data URI） |
| iconFile | string | "" | 否 | 图标文件路径（相对icon/目录） |

#### @DeviceFunction — 功能方法

**目标**：方法（Method） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 功能名称（唯一标识） |
| titleCN | string | — | 是 | 中文标题 |
| titleEN | string | — | 是 | 英文标题 |
| description | string | "" | 否 | 功能描述 |
| categoryCN | string | "" | 否 | 中文分类 |
| categoryEN | string | "" | 否 | 英文分类 |
| defaultPeriod | int | 60 | 否 | 默认执行时长（秒） |
| formJson | string | "" | 否 | 动态表单JSON定义 |
| iconBlack | string | "" | 否 | 深色模式图标（Base64） |
| iconWhite | string | "" | 否 | 浅色模式图标（Base64） |
| iconFileBlack | string | "" | 否 | 深色模式图标文件路径 |
| iconFileWhite | string | "" | 否 | 浅色模式图标文件路径 |

#### @DeviceOperation — 操作方法

**目标**：方法（Method） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 操作名称 |
| titleCN | string | — | 是 | 中文标题 |
| titleEN | string | — | 是 | 英文标题 |
| description | string | "" | 否 | 操作描述 |
| formJson | string | "" | 否 | 动态表单JSON定义 |

#### @DeviceSet — 设置方法

**目标**：方法（Method） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 参数名称 |
| titleCN | string | — | 是 | 中文标题 |
| titleEN | string | — | 是 | 英文标题 |
| type | string | "input" | 否 | 输入类型：`input`, `select` |
| unit | string | "" | 否 | 参数单位 |
| description | string | "" | 否 | 参数描述 |

#### @DeviceGet — 获取方法

**目标**：方法（Method） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 状态名称 |
| titleCN | string | — | 是 | 中文标题 |
| titleEN | string | — | 是 | 英文标题 |
| type | string | "string" | 否 | 值类型：`boolean`, `string`, `int`, `float` |
| unit | string | "" | 否 | 单位 |
| description | string | "" | 否 | 状态描述 |

#### @DeviceEnterExit — 进出方法

**目标**：方法（Method） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| name | string | — | 是 | 进出操作名称 |
| titleCN | string | — | 是 | 中文标题 |
| titleEN | string | — | 是 | 英文标题 |
| description | string | "" | 否 | 操作描述 |

#### @DeviceNest — 位置声明

**目标**：方法（Java）或属性（C#） | **保留策略**：运行时

| 属性 | 类型 | 默认值 | 必填 | 说明 |
|------|------|--------|------|------|
| order | int | 0 | 否 | 位置排序序号（从小到大排列） |

**返回类型**：被标注的方法/属性须返回 `EquipmentNest` 对象，由驱动基类在 `getRegisterInfo()` 调用时通过反射扫描并收集。多个 `@DeviceNest` 方法/属性按 `order` 升序排列后，汇总到 `RegisterInfo.AdvancedInfo.equipmentNests` 列表中。

### 11.4 异常处理规范

- 所有对外接口须捕获异常并转换为标准 `Result<T>` 错误响应，禁止向调用方暴露堆栈信息；
- 业务异常使用预定义错误码（见附录A）；
- 通信异常须设置心跳状态为相应异常值（见第14节）。

### 11.5 日志规范

- 驱动须支持分级日志（DEBUG / INFO / WARN / ERROR），生产环境默认INFO级别；
- 日志须包含时间戳（ISO 8601）、级别、来源类名、消息内容；
- 功能执行日志须关联 `instructionId`，实现全链路可追溯。

### 11.6 日志提供者（LoggerProvider）

SDK提供可插拔的日志工厂机制（`LoggerProvider` / `LoggerProvider`），默认使用控制台输出：

| 配置项 | 说明 |
|--------|------|
| 默认日志级别 | DEBUG（SDK内部）/ INFO（根日志） |
| 日志格式 | `yyyy-MM-dd HH:mm:ss.SSS [thread] level logger - msg` |
| 文件日志 | 滚动文件追加器，路径 `logs/driver.log`，按日滚动，保留30天 |
| 可替换性 | 可通过 `setLoggerFactory()` 注入自定义日志工厂（如Logback、Log4j、Serilog等） |

**取值依据**：保留30天——覆盖一个完整实验周期（典型为1-2周）并留有余量用于问题回溯；可替换性设计——不同企业可能已有统一的日志采集体系，SDK不应强制绑定特定日志实现。

### 11.7 图标管理（IconHelper）

SDK内置图标管理工具（`IconHelper` / `IconHelper`），提供设备图标与功能图标的加载、缓存和默认回退机制：

| 功能 | 说明 |
|------|------|
| 加载顺序 | 文件系统（`icon/`目录） → 类路径/嵌入资源 → 内置默认SVG图标 |
| 支持格式 | PNG, JPG, JPEG, GIF, BMP, SVG, WebP, ICO |
| 默认设备图标 | 内置"设备默认图片.png"（Base64 data URI） |
| 默认功能图标（深色） | 内置"icon_组件_默认图标_黑色版.png" |
| 默认功能图标（浅色） | 内置"icon_组件_默认图标_白色版.png" |
| MIME类型检测 | 根据文件扩展名自动匹配，默认 `image/png` |
| 图标目录路径 | 可配置 `IconFolderPath`，默认为SDK自动探测的 `icon/` 目录 |

**取值依据**：支持8种常见图像格式——覆盖Web（PNG/SVG/WebP）、通用（JPG/GIF/BMP）、Windows图标（ICO）三大类，确保各种来源的设备图标均可加载；三级回退策略——文件系统→类路径→内置默认，确保任一环境下均有可用图标。

### 11.8 驱动实例管理（DeviceDriverFactory）

SDK提供全局驱动实例注册与查找机制（C#: `DeviceDriverFactory`），支持多设备多驱动场景：

| 操作 | 说明 |
|------|------|
| `CreateDriver<T>(config)` | 通过反射创建驱动实例并扫描注册注解 |
| `RegisterDriver(deviceId, driver)` | 按设备ID注册驱动实例至全局注册表 |
| `GetDriver(deviceId)` | 按设备ID获取已注册驱动实例 |
| `UnregisterDriverAsync(deviceId)` | 异步注销驱动并释放资源 |
| `ClearAllAsync()` | 清空所有注册驱动并释放全部资源 |

**线程安全**：内部使用 `ConcurrentDictionary`，支持并发注册与注销。

### 11.9 配置验证（HttpsOptions.validate()）

驱动启动前须通过 `HttpsOptions.validate()` 对配置进行合法性校验：

| 校验项 | 规则 |
|--------|------|
| 端口范围 | 1–65535，超出抛出 `IllegalArgumentException` |
| 证书路径 | HTTPS模式下证书路径必须配置且文件存在 |
| 信任库路径 | 如配置独立信任库，路径必须有效且文件存在 |
| TLS协议 | 启用HTTPS时必须是目标运行时明确支持的TLS 1.2或TLS 1.3，不允许TLS 1.0/1.1 |
| 客户端认证模式 | 必须是 `NONE`、`WANT` 或 `NEED`；启用客户端证书时必须配置可验证的信任来源 |

校验失败时驱动须拒绝启动，不得以降级模式（如自动切换到HTTP）运行。

---

## 12 配置管理规范

### 12.1 服务端配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| port | int | 8080 | 服务监听端口 |
| host | string | "localhost" | 监听地址，"0.0.0.0"表示所有网络接口 |
| useHttps | boolean | false | 是否显式启用HTTPS |
| certificate.path | string | — | 服务端证书路径（PKCS12 .pfx/.p12） |
| certificate.password | string | — | 证书密码，建议使用环境变量 |
| certificate.type | string | "PKCS12" | 密钥库类型 |
| certificate.alias | string | — | 密钥别名（证书包含多个密钥时指定） |
| trustStore.path | string | — | 信任库路径 |
| trustStore.password | string | — | 信任库密码，建议使用环境变量 |
| trustStore.type | string | "PKCS12" | 信任库类型 |
| ssl.protocol | string | "TLSv1.2" | TLS协议版本（启用HTTPS时） |
| ssl.enabledProtocols | string[] | ["TLSv1.2"] | 启用的协议列表 |
| ssl.ciphers | string[] | [] | 留空时使用运行平台安全策略 |
| clientAuth.mode | string | "none" | 客户端认证模式 |
| clientAuth.trustedThumbprints | string[] | — | 受信客户端证书指纹列表 |
| clientAuth.trustedIssuers | string[] | — | 受信签发CA证书指纹列表 |

**取值依据**：SDK默认HTTP/8080并关闭客户端认证，保证首次配置不依赖证书；生产部署按6.2显式启用HTTPS/mTLS。`host` 默认localhost遵循最小暴露原则，部署时按网络策略修改。

### 12.2 设备通信配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| deviceId | string | — | 设备实例唯一标识 |
| communicationType | string | — | TCP / HTTP / Serial |
| tcp | object | — | TCP的host、port、connect/read/write超时 |
| http | object | — | HTTP的baseUrl及超时 |
| serial | object | — | Serial的port、baudRate、dataBits、stopBits、parity、flowControl、DTR/RTS、编码及超时 |
| connections | object | — | 多连接字典；键由驱动定义，可配置type、default、resourceGroup、maxConcurrency及厂商settings |
| deviceCallResources | int | 1 | DLL/API真实调用资源，与业务并行和传输锁分离 |
| deviceCallTimeoutMs | int | 30000 | 获取DLL/API调用资源的超时 |

**取值依据**：
- `connectTimeoutMs` 默认5000ms；读写超时按设备协议和动作时长分别配置；
- `baudRate` 默认9600——工业设备最常见串口波特率，兼容大多数传统设备。
- TCP/Serial/Modbus请求–响应连接的物理并发固定为1；HTTP/process/DLL/API仅在厂商明确支持时增加。

### 12.3 回调配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| url | string | — | 回调地址 |
| timeoutMs | int | 30000 | 回调超时（ms） |
| enabled | boolean | false | 是否启用回调；启用时URL必须有效 |

**取值依据**：
- `timeoutMs` 默认30000ms——覆盖平台在正常负载下处理回调请求的95分位耗时（实测< 5s）的6倍安全余量；

---

## 13 安全与传输加密

### 13.1 安全等级划分

按部署环境和风险选择安全配置，默认HTTP仅用于本机开发或有等效边界保护的受控内网：

| 环境 | 安全要求 | 依据 |
|------|---------|------|
| 本机开发 | HTTP/127.0.0.1，关闭回调；需要验证TLS时再启用测试证书 | 降低首次配置门槛并限制暴露范围 |
| 集成测试 | 与目标生产安全配置一致，至少完成TLS 1.2真实握手；需要时验证mTLS | 在发布前发现证书链、JVM和操作系统策略差异 |
| 生产环境 | HTTPS；TLS 1.2基线，验证后推荐TLS 1.3；高风险网络推荐mTLS NEED | 保障实验数据与设备控制链路，防止未授权接入 |

### 13.2 证书管理

| 证书类型 | 格式 | 用途 |
|---------|------|------|
| 服务端证书 | PKCS12 (.pfx/.p12) | 驱动端身份验证 |
| 客户端证书 | X.509 | 平台身份验证 |
| 信任锚 | PKCS12 / JKS | 验证对端证书有效性 |

### 13.3 安全配置参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| serverCertificatePath | string | — | 服务端证书路径（PKCS12 .pfx/.p12） |
| serverCertificatePassword | string | — | 证书密码 |
| keyStoreType | string | "PKCS12" | 密钥库类型 |
| keyAlias | string | — | 密钥别名（证书中包含多个密钥时指定） |
| trustStorePath | string | — | 信任库路径（空则复用KeyStore中证书） |
| trustStorePassword | string | — | 信任库密码 |
| trustStoreType | string | "PKCS12" | 信任库类型 |
| protocol | string | "TLSv1.2" | SSL/TLS协议版本 |
| enabledProtocols | string[] | ["TLSv1.2"] | 启用的协议列表 |
| ciphers | string[] | [] | 留空时使用运行平台安全策略 |
| clientAuth | enum | NONE | 客户端认证模式：NONE/WANT/NEED |
| requireClientCertificate | boolean | false | 是否要求客户端证书 |
| trustedClientThumbprints | string[] | — | 受信客户端证书指纹列表 |
| trustedIssuerThumbprints | string[] | — | 受信签发CA证书指纹列表 |

---

## 14 心跳与健康监测机制

### 14.1 心跳状态定义

| 状态码 | 名称 | 含义 |
|--------|------|------|
| 0 | Normal | 驱动已初始化，设备已连接，无功能在执行 |
| 1 | DriverAbnormal | 驱动内部异常 |
| 2 | DriverOverTime | 驱动响应超时 |
| 3 | EquipmentAbnormal | 设备通信断开或设备报错 |
| 4 | EquipmentError | 设备不可恢复错误 |
| 5 | EquipmentOverTime | 设备操作超时 |
| 6 | Monitoring | 设备正在执行功能 |

### 14.2 心跳判定逻辑

驱动按以下优先级判定心跳状态（序号越小优先级越高）：

1. 驱动未初始化 → Monitoring（6）
2. 设备通信断开 → EquipmentAbnormal（3）
3. 功能资源被占用 → Monitoring（6）
4. 以上均不满足 → Normal（0）

### 14.3 心跳周期与超时

| 指标 | 要求值 | 取值依据 |
|------|--------|----------|
| 平台轮询周期 | 5~30 s | 下限5s：避免网络拥塞；上限30s：设备异常发现延迟不超过典型实验步骤时长（60s）的50% |
| 离线判定阈值 | 连续3次无响应 | 统计可靠性准则——单次丢包率 < 1%，3次连续丢失概率 < 10⁻⁶，有效排除网络抖动 |
| 驱动健康状态刷新周期 | 与轮询请求同步 | 按需计算，避免后台定时器额外资源开销 |

---

## 15 回调与异步通知机制

### 15.1 回调流程

功能接口（`/Function`）采用异步执行模式：

1. 平台 POST `/Function`；
2. 驱动同步返回 200 + `"Function accepted"`（处理时限 ≤ 200ms）；
3. 驱动后台线程执行功能逻辑；
4. 执行完成后，驱动POST `Finish` 对象到平台回调URL；
5. 平台接收回调，更新指令状态。

**指标依据**：200ms同步响应上限——基于HTTP/1.1请求在局域网环境下的典型往返延时（< 10ms）的20倍安全余量，确保平台不因等待驱动响应而阻塞。

### 15.2 回调失败处理

回调失败（网络异常、超时、非200响应）时驱动应：
1. 记录ERROR级别日志；
2. 按配置的重试策略执行重试（默认3次，递增间隔）；
3. 不因回调失败影响后续功能执行。

---

## 16 质量管理规范

### 16.1 驱动测试要求

| 测试类型 | 覆盖率要求 | 依据 |
|---------|-----------|------|
| 单元测试 | 核心逻辑 ≥ 80% | 行业通用基准（IEEE 829），核心逻辑包含通信、信号量、注解扫描 |
| 接口测试 | 7个标准端点100%覆盖 | 接口为驱动对外契约，须全部验证 |
| 集成测试 | 与真实设备联调 ≥ 1次 | 确保通信层与实际物理设备兼容 |

### 16.2 性能指标

| 指标 | 要求值 | 依据 |
|------|--------|------|
| /Info 响应时间 | ≤ 200ms (P95) | 同步接口基准，保障平台注册流程流畅 |
| /HeartBeat 响应时间 | ≤ 100ms (P95) | 高频调用接口（5~30s周期），需低延迟 |
| /Function 同步响应时间 | ≤ 200ms | 仅做参数校验与任务入队，不执行实际功能 |
| 并发功能数 | ≥ functionalResources 配置值 | 不应低于声明能力 |

### 16.3 兼容性要求

- **编程语言**：不绑定特定语言，提供Java 8+ 与 .NET 6+ 参考实现；
- **操作系统**：支持Windows 10+、Linux（内核4.15+）、macOS 12+；
- **向后兼容**：新版本SDK须兼容旧版配置格式，废弃字段保留至少一个大版本的过渡期。

---

## 17 部署与运维规范

### 17.1 部署要求

- 驱动须支持独立进程部署，不依赖特定容器或应用服务器；
- 证书文件与密钥不得硬编码在源码中，须通过配置文件或环境变量注入；
- 生产部署须关闭调试端点与冗余日志输出。

### 17.2 版本管理

驱动版本号须遵循语义化版本规范（Semantic Versioning 2.0.0）：`主版本.次版本.修订号`。

### 17.3 监控与告警

| 监控项 | 采集方式 | 告警阈值 | 依据 |
|--------|---------|---------|------|
| 驱动进程存活 | 心跳连续失败次数 | ≥ 3次 | 同14.3离线判定阈值 |
| 设备连接状态 | heartBeatStatus | ≥ 3（EquipmentAbnormal） | 设备异常需立即关注 |
| 功能执行时长 | Finish回调时间戳差值 | > 2 × functionDefaultPeriod | 2倍默认时长为异常信号 |

---

# 第三部分：专项规范

## 18 生物安全规范

### 18.1 生物安全等级

合成生物制造实验涉及的生物安全风险参照《中华人民共和国生物安全法》及WHO《实验室生物安全手册》（第四版）分级：

| 安全等级 | 定义 | 适用场景 | 设备要求 |
|---------|------|---------|---------|
| BSL-1 | 低风险：已知不引起健康成人疾病的微生物 | 常规分子克隆、细胞培养 | 标准实验设备，无特殊隔离要求 |
| BSL-2 | 中等风险：可引起人类疾病但通常可治疗 | 病原微生物操作、基因编辑 | 设备须支持表面消毒、气溶胶防控 |
| BSL-3 | 高风险：可引起严重或致死性疾病 | 高致病性病原体操作 | 设备须具备物理隔离、负压环境支持 |
| BSL-4 | 极高风险：致命且无有效防治手段 | 烈性病原体操作 | 不在本标准适用范围内 |

### 18.2 生物危害防护

- **物理隔离**：BSL-2及以上等级设备须记录安全柜、隔离器运行状态，异常状态（如气流异常、密封失效）须通过心跳状态码上报告警；
- **消毒灭菌**：设备须声明适用的消毒方式（UV、化学消毒剂、高压蒸汽），驱动须提供消毒/灭菌操作方法；
- **泄漏应急处置**：驱动配置中须包含生物泄漏应急联系人信息与处置流程描述。

### 18.3 生物材料管理

- 设备涉及生物材料存储或处理时，驱动注册信息（`/Info`）须声明适用的生物材料类别（菌株、细胞系、核酸、蛋白质等）；
- 设备须记录生物材料使用日志（样本ID、操作时间、操作类型），保存期 ≥ 5年（参照《生物安全法》第三十八条关于实验记录保存的要求）。

### 18.4 废弃物处理

设备产生的生物废弃物（培养物、废弃耗材、清洗废液）须在注册信息中标注废弃物类型与推荐处理方式，驱动操作集中须包含废弃物处理相关操作。

---

## 19 伦理规范

### 19.1 伦理审查要求

- 涉及生物样本（尤其是人类来源样本）的实验流程，平台须支持关联伦理审查批准号（批准号通过 `/Function` 请求中的扩展字段传入），驱动端记录该批准号至操作日志；
- 涉及动物实验的设备操作，须在功能描述中明确标注动物实验标识，驱动日志须完整记录实验起止时间与关键参数。

### 19.2 数据伦理

- **知情同意**：涉及人类样本数据的采集、传输与存储，平台须确认伦理批准与知情同意已完备，驱动须在元数据中记录同意状态；
- **隐私保护**：驱动不得采集超出功能必要的用户个人信息；设备操作日志中涉及的样本溯源信息须脱敏存储（仅保留脱敏后的样本编码）；
- **算法公平性**：设备驱动的自动化决策逻辑（如自动参数校准）须可解释，不得因样本来源、批次等非实验因素产生系统性偏差。

### 19.3 人工智能伦理（AI Ethics）

当设备驱动集成AI/ML模型进行决策（如自动分类、异常检测、实验参数推荐）时，须遵守：

- **可解释性**：模型的决策输出须附带置信度或解释说明。置信度阈值默认 ≥ 0.85（基于分类模型通用基准——F1 ≥ 0.85视为可靠，参见Powers, D.M.W., JMLR 2011）；
- **人机协同**：高危操作（如涉及BSL-3及以上生物安全等级的决策、药物剂量相关参数设定）须保留人工确认环节，驱动不得全自动执行；
- **模型版本追溯**：驱动注册信息中须声明所集成AI模型的名称、版本、训练数据集摘要，确保决策可追溯、可复现。

### 19.4 实验伦理

- 设备功能涉及活体生物的，须在功能描述中注明"本功能涉及活体生物操作"，提醒操作者审慎执行；
- 支持设置实验参数上下限，防止因误操作输入极端参数造成生物体不可逆损伤。

---

## 20 应急预案

### 20.1 应急响应分级

| 级别 | 定义 | 响应时效要求 | 依据 |
|------|------|-------------|------|
| I级（特别严重） | 人身安全受威胁、BSL-3及以上生物泄漏、重大设备损坏 | 即时响应，≤ 30s触发急停 | 参照GB/T 24353-2009风险管理原则中的"不可接受风险"应对时效 |
| II级（严重） | 设备失控、生物污染扩散、数据大规模丢失 | ≤ 5min启动处置 | 参照实验室通用应急处置时效 |
| III级（一般） | 单设备故障、通信中断、单次操作失败 | ≤ 30min响应 | 运维级故障标准响应时限 |

### 20.2 设备故障应急预案

| 故障类型 | 检测方式 | 自动处置 | 人工处置要求 |
|---------|---------|---------|-------------|
| 设备通信中断 | 心跳状态 ≥ 3 | 标记设备不可用，暂停该设备新任务 | 检查物理连接与设备电源 |
| 设备操作超时 | 心跳状态 = 5 | 驱动自动尝试复位操作 | 如3次复位无效，通知运维人员 |
| 设备不可恢复错误 | 心跳状态 = 4 | 自动急停（如支持），驱动进入待维护状态 | 联系设备制造商 |

### 20.3 生物安全事件应急预案

- 驱动须支持接收平台下发的**全局急停指令**（通过 `/Operation` 端点，operationName = "EmergencyStop"），收到指令后须在 ≤ 200ms内完成：
  1. 停止所有正在执行的功能；
  2. 关闭设备可能的气溶胶产生源；
  3. 上报设备当前安全状态。
- 全平台急停指令须在 ≤ 2s内覆盖所有在管设备（基于200驱动 × 10ms转发延迟的工程上限估算）。

### 20.4 数据安全应急预案

- 驱动配置中须声明数据备份策略（备份周期、备份位置、保留份数），推荐默认配置：每日增量备份、保留 ≥ 7份（覆盖一周）；
- 数据恢复时间目标（RTO） ≤ 4h，数据恢复点目标（RPO） ≤ 24h。

### 20.5 应急演练要求

- I级、II级应急预案每半年至少演练一次；
- 演练结果须形成书面报告，驱动日志须保留演练操作记录（带演练标识，与真实事件区分）。

---

## 21 系统统一接口

### 21.1 平台间互操作接口

CIAI平台与其他外部系统（如实验室信息管理系统LIMS、制造执行系统MES、企业资源计划系统ERP）交互时，须通过统一的网关接口进行：

| 接口类型 | 方向 | 协议 | 数据格式 | 用途 |
|---------|------|------|---------|------|
| 任务下发接口 | 外部 → CIAI | REST/HTTPS | JSON | 外部系统向CIAI平台提交实验任务 |
| 结果回传接口 | CIAI → 外部 | REST/HTTPS | JSON | CIAI平台向外部系统反馈实验结果 |
| 状态查询接口 | 双向 | REST/HTTPS | JSON | 外部系统查询设备/任务实时状态 |

### 21.2 统一身份认证接口

- 所有CIAI平台对外接口须通过统一身份认证服务（兼容OAuth 2.0 / API Key）进行访问控制；
- 平台与驱动之间的mTLS双向认证为设备层认证，平台对外接口须额外进行应用层鉴权，两层不互相替代。

### 21.3 统一数据交换格式

平台间数据交换采用统一的JSON Schema定义（见附录B），所有外部接口须遵循该Schema，确保不同CIAI实例之间的数据可互操作。

---

## 22 数据共享规则

### 22.1 数据分类分级

| 数据类别 | 级别 | 示例 | 共享策略 |
|---------|------|------|---------|
| 设备运行数据 | 低敏感 | 心跳信息、设备型号 | 平台内公开 |
| 实验过程数据 | 中敏感 | 功能执行记录、参数设置历史 | 授权共享，需脱敏 |
| 实验结果数据 | 中敏感 | 检测读数、分析结果 | 授权共享 |
| 样本身份数据 | 高敏感 | 样本ID、患者信息 | 严格授权，默认不共享 |
| 知识产权数据 | 机密 | 实验设计方案、专有算法参数 | 禁止默认共享 |

### 22.2 数据共享原则

- **最小必要原则**：共享的数据范围不超过完成指定任务所必需的最小数据集；
- **授权知情原则**：数据共享前须获得数据提供方的明确授权（通过伦理批准号或数据共享协议编号关联）；
- **可审计原则**：所有数据共享操作须记录日志（请求方、接收方、共享字段范围、时间戳），日志保存期 ≥ 5年。

### 22.3 数据脱敏要求

高敏感数据在共享前须执行脱敏处理：

- 身份标识（样本ID、患者ID）：使用不可逆哈希（SHA-256加盐）；
- 时间戳精度：降低至日期级别（去除时分秒），防止通过时间关联还原身份；
- 位置信息（如设备具体安装位置）：泛化至楼层级别。

### 22.4 数据溯源与审计

- 每条实验数据须包含以下溯源元数据：`设备ID → 驱动版本 → 功能名称 → 指令ID → 时间戳 → 操作者ID`；
- 数据共享日志支持按请求方、时间范围、数据类别查找审计。

---

## 23 知识产权

### 23.1 知识产权归属

- 本标准本身的知识产权归CIAI技术委员会所有；
- 基于本标准开发的驱动软件的知识产权归开发者所有，但其中引用的标准接口定义、数据模型和协议规范不构成知识产权限制；
- 设备制造商提供的专有通信协议，其知识产权仍归设备制造商所有，驱动开发者在实现时应遵守相关授权协议。

### 23.2 开源许可合规

- 驱动开发过程中使用开源软件的，须在注册信息的 `author` 或扩展字段中声明所引用的主要开源组件及其许可类型；
- 禁止使用与产品分发方式冲突的开源许可（如GPL v3用于闭源分发场景），建议优先采用MIT、Apache 2.0等宽松许可；
- 禁止引入存在已知安全漏洞（CVE评分 ≥ 7.0）且无修复版本的开源依赖。

**量化依据**：CVE评分7.0为CVSS v3.1标准中的"高危"分界线——≥ 7.0表示漏洞可利用性高或影响范围大，未修复的高危依赖可能被远程利用导致设备控制权丧失。

### 23.3 专利与技术秘密

- 设备驱动开发中涉及的算法、流程改进应首先评估专利申请可行性，不宜在未申请前通过标准文档或开源代码公开；
- 通信协议中涉及设备制造商技术秘密的参数（如校准算法、控制策略），驱动应以黑箱方式调用，不得逆向工程或公开。

### 23.4 商标与标识

- "CIAI"标识及本标准名称的使用须遵守CIAI技术委员会的标识使用指南；
- 符合本标准的驱动产品可在文档中声明"CIAI-STD-2.0兼容"（Compliant with CIAI-STD-2.0），但须通过标准符合性验证。

---

# 附录

## 附录A：错误码定义

| 错误码 | 常量名 | HTTP状态码 | 含义 |
|--------|--------|-----------|------|
| `message.common.success` | SUCCESS | 200 | 操作成功 |
| `message.common.failed` | FAILED | 200 | 操作失败（业务层） |
| `message.common.unauthorized` | UNAUTHORIZED | 401 | 未授权（TLS认证失败或权限不足） |
| `message.common.forbidden` | FORBIDDEN | 403 | 禁止访问（认证通过但无操作权限） |
| `message.common.notfound` | NOT_FOUND | 404 | 资源未找到 |
| `message.common.badrequest` | BAD_REQUEST | 400 | 请求参数格式错误 |
| `message.common.parameters.missing` | PARAMETERS_MISSING | 400 | 必填参数缺失 |
| `message.common.timeout` | TIMEOUT | 504 | 操作超时 |
| `message.common.server.error` | SERVER_ERROR | 500 | 驱动服务内部错误 |
| `message.common.error` | ERROR | 500 | 通用内部错误 |

---

## 附录B：JSON Schema参考

### B.1 FunctionData Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["functionName", "instructionId"],
  "properties": {
    "functionName": { "type": "string" },
    "instructionId": { "type": "string" },
    "nestId": { "type": "string" },
    "equipmentName": { "type": "string" },
    "userId": { "type": "string" },
    "taskId": { "type": "string" },
    "functionParam": { "type": "object" },
    "labwareInfo": {
      "type": "object",
      "properties": {
        "LabwareName": { "type": "string" },
        "capacity": { "type": "string" },
        "capacityRow": { "type": "integer" },
        "capacityColumn": { "type": "integer" }
      }
    }
  }
}
```

### B.2 Finish Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["completion"],
  "properties": {
    "completion": { "type": "string", "enum": ["finish", "error"] },
    "errorMsg": { "type": "string" },
    "instructionId": { "type": "string" },
    "nestId": { "type": "string" },
    "resultOutput": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string", "description": "结果键名" },
          "resultData": { "description": "结果值" }
        }
      }
    }
  }
}
```

### B.3 Result Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["code", "message"],
  "properties": {
    "code": { "type": "string" },
    "message": { "type": "string" },
    "data": {}
  }
}
```

---

## 附录C：代码示例

### C.1 Java驱动示例

```java
@DeviceDriver(
    name = "温控设备",
    nameEN = "TemperatureController",
    model = "TC-100",
    manufacturer = "示例厂商",
    version = "1.0.0",
    equipmentType = 1,
    functionalResources = 1
)
public class TemperatureDriver extends DeviceDriverBase {

    @DeviceFunction(
        name = "Heat",
        titleCN = "加热",
        titleEN = "Heat",
        description = "加热到指定温度",
        categoryCN = "温度控制",
        categoryEN = "Temperature Control",
        defaultPeriod = 60,
        formJson = "{\"fields\":[{\"name\":\"temperature\",\"type\":\"number\",\"label\":\"目标温度\"}]}"
    )
    public Result<Finish> heat(FunctionData data) {
        double targetTemp = parseParam(data.getFunctionParam(), 37.0);
        sendCommand("SET_TEMP", targetTemp);
        waitForCompletion();
        Finish finish = Finish.success();
        finish.setInstructionId(data.getInstructionId());
        finish.setResultOutput(List.of(
            new Finish.ResultOutput("temperature", currentTemperature)
        ));
        return Result.success(finish);
    }
}
```

### C.2 C#/.NET驱动示例

```csharp
[DeviceDriver(
    Name = "温控设备",
    NameEN = "TemperatureController",
    Model = "TC-100",
    Manufacturer = "示例厂商",
    EquipmentType = 1,
    FunctionalResources = 1
)]
public class TemperatureDriver : DeviceDriverBase
{
    [DeviceFunction(
        Name = "heat",
        TitleCN = "加热",
        TitleEN = "Heat",
        Description = "加热到指定温度",
        CategoryCN = "温度控制",
        DefaultPeriod = 120,
        FormJson = @"{""fields"":[{""name"":""targetTemp"",""type"":""number"",""label"":""目标温度""}]}"
    )]
    public async Task<Finish> HeatAsync(FunctionData data)
    {
        var param = DeserializeParam<HeatParam>(data.FunctionParam);
        await SendAsync(Encoding.UTF8.GetBytes($"SET_TEMP:{param.TargetTemp}"));
        return new Finish
        {
            Completion = "finish",
            InstructionId = data.InstructionId,
            NestId = data.NestId
        };
    }
}
```

### C.3 启动配置示例（YAML）

```yaml
server:
  port: 8080
  host: "0.0.0.0"
  useHttps: false

device:
  communicationType: "TCP"
  tcp:
    host: "192.168.1.100"
    port: 5000
    connectTimeoutMs: 5000

callback:
  url: ""
  timeoutMs: 30000
  enabled: false
```

生产HTTPS/mTLS配置使用两套SDK `application.sample.yml` 中的完整注释模板；仅在
`useHttps: true` 后取消证书、信任库和TLS段的注释。

---

## 附录D：补充规范详情

### D.1 YAML配置完整层级结构

驱动支持通过 `application.yml` 文件配置全部参数，完整层级结构如下：

```
DriverConfig
├── server (ServerConfig)
│   ├── port: int (默认8080)
│   ├── host: string (默认"localhost")
│   ├── useHttps: boolean (默认false)
│   ├── certificate (CertificateConfig)
│   │   ├── path: string
│   │   ├── password: string
│   │   ├── type: string (默认"PKCS12")
│   │   └── alias: string (密钥别名)
│   ├── trustStore (TrustStoreConfig)
│   │   ├── path: string
│   │   ├── password: string
│   │   └── type: string (默认"PKCS12")
│   ├── ssl (SslConfig)
│   │   ├── protocol: string (默认"TLSv1.2")
│   │   ├── enabledProtocols: string[]
│   │   └── ciphers: string[]
│   └── clientAuth (ClientAuthConfig)
│       ├── mode: string (默认"none")
│       ├── enabled: boolean (默认false)
│       ├── trustedThumbprints: string[]
│       └── trustedIssuers: string[]
├── callback (CallbackConfig)
│   ├── url: string
│   ├── timeoutMs: int (默认30000)
│   └── enabled: boolean (默认false)
└── device (DeviceConfigSection)
    ├── deviceId: string
    ├── communicationType: string (TCP/HTTP/Serial)
    ├── tcp (TcpConfig)
    │   ├── host: string
    │   ├── port: int
    │   └── timeoutMs: int (默认5000)
    ├── http (HttpConfig)
    │   ├── baseUrl: string
    │   └── timeoutMs: int (默认30000)
    └── serial (SerialConfig)
        ├── port: string
        ├── baudRate: int (默认9600)
        ├── dataBits: int (默认8)
        ├── stopBits: int (默认1)
        ├── parity: string (默认"none")
        └── timeoutMs: int (默认5000)
```

配置加载：`YamlConfigLoader` 依次尝试文件系统路径 → 类路径/嵌入资源 → 返回null，由 `DriverHost` 根据返回值决定是否使用代码配置作为后备。

### D.2 架构示意图

```
┌─────────────────────────────────────────────────────┐
│                   调度平台 (Platform)                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │ 任务编排  │  │ 设备管理  │  │ 状态监控  │           │
│  └─────┬────┘  └─────┬────┘  └─────┬────┘           │
│        └──────────┬───┴───────────┬┘                 │
└───────────────────┼───────────────┼──────────────────┘
                    │  HTTPS/HTTP   │
        ┌───────────┼───────────────┼───────────┐
        │           │               │           │
┌───────┴──┐  ┌─────┴────┐  ┌──────┴───┐  ┌────┴─────┐
│ 温控驱动  │  │ 移液驱动  │  │ 孵育驱动  │  │ 检测驱动  │
│ :8443    │  │ :8444    │  │ :8445    │  │ :8446    │
└────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘
     │             │             │             │
  [温控仪]     [移液器]       [孵育箱]      [读数器]
```

### D.3 交互时序图

```
平台                          驱动服务                      物理设备
 │                              │                            │
 │──── GET /Info ────────────>│                            │
 │<─── RegisterInfo ──────────│                            │
 │                              │                            │
 │──── GET /HeartBeat ──────>│                            │
 │<─── HeartBeatInfo ─────────│                            │
 │                              │                            │
 │──── POST /Function ──────>│                            │
 │<─── 200 Accepted ──────────│                            │
 │                              │──── 设备通信 ────────────>│
 │                              │<─── 设备响应 ─────────────│
 │<─── Callback(Finish) ──────│                            │
```

### D.4 位置描述（EquipmentNest）完整字段

| 字段 | 类型 | 说明 |
|------|------|------|
| nestName | string | 位置名称 |
| labwareType | string | 兼容耗材类型（逗号分隔） |
| nestPostures | string | 位置姿态编码 |
| nestAccessibility | int | 可访问性（0/1） |
| nestDescription | string | 位置描述 |
| nestHeight | float | 位置高度（mm） |
| nestCoordinate | string | 位置坐标标识 |
| nestColumnOrder | int | 列排列顺序 |
| nestColumnCo | int | 列数 |
| nestLayerCo | int | 层数 |
| typeOnly | int | 是否仅限指定耗材（0/1） |
| nestIsDestination | int | 是否可作为目标位置（0/1） |
| transitionNest | string | 过渡位置名称 |
| postEnterFormJsonStructure | string | 进样后表单JSON |
| preEnterFormJsonStructure | string | 进样前表单JSON |
| postExitFormJsonStructure | string | 出样后表单JSON |
| preExitFormJsonStructure | string | 出样前表单JSON |

### D.5 动态表单兼容格式（嵌套结构）

```json
{
  "nest": [
    {
      "key": "targetTemp",
      "description": { "label": "目标温度(°C)", "type": "number" }
    },
    {
      "key": "duration",
      "description": { "label": "持续时间(秒)", "type": "int" }
    }
  ]
}
```

### D.6 响应示例（GET /Info）

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": {
    "basicInfo": {
      "equipmentName": "温控设备",
      "equipmentNameEN": "TemperatureController",
      "equipmentModel": "TC-100",
      "equipmentManufacturer": "示例厂商",
      "version": "1.0.0",
      "equipmentType": 1,
      "functionalResources": 1,
      "canE_Stop": 1,
      "runtimeAccessibility": 1,
      "parallelizability": 0
    },
    "advancedInfo": {
      "equipmentFunctions": [],
      "equipmentOperations": [],
      "equipmentSetInfos": [],
      "equipmentGetInfos": [],
      "equipmentNests": [],
      "equipmentEnterAndExit": {}
    }
  }
}
```

---

## 附录E：量化指标依据汇总

| 序号 | 章节 | 量化指标 | 要求值 | 取值依据 |
|------|------|---------|--------|----------|
| 1 | 4.1 | 单驱动最小内存占用 | ≤ 64 MB | .NET 6 / Java 8最小运行时实测 |
| 2 | 4.1 | 单驱动启动时间 | ≤ 10 s | 全流程95分位耗时 |
| 3 | 4.1 | 单平台最大管理驱动数 | ≥ 200 | 大型平台设备上限2倍冗余 |
| 4 | 5.1 | functionalResources默认值 | 1 | 单数设备物理约束 |
| 5 | 5.1 | parallelizability默认值 | 0 | 保守防冲突策略 |
| 6 | 6.1 | SDK默认端口 | 8080 | 无证书首次运行；生产端口由部署策略确定 |
| 7 | 6.2 | TLS版本 | TLS 1.2基线，验证后推荐TLS 1.3 | 兼容Java 8/.NET 6/8并禁止TLS 1.0/1.1 |
| 7a | 6.2 | 客户端认证 | 默认NONE；高风险部署推荐mTLS NEED | 在身份保证与证书运维成本间按风险选择 |
| 8 | 12.2 | connectTimeoutMs默认值 | 5000ms | 局域网连接建立的保守超时基线 |
| 9 | 12.3 | callbackTimeoutMs默认值 | 30000ms | 平台处理回调95分位 × 6 |
| 11 | 14.3 | 心跳轮询周期 | 5~30 s | 拥塞控制与异常发现时延平衡 |
| 12 | 14.3 | 离线判定阈值 | 连续3次 | 单次丢包率 < 1%，3次连续概率 < 10⁻⁶ |
| 13 | 15.1 | 同步响应上限 | ≤ 200ms | 局域网HTTP往返 × 20安全余量 |
| 14 | 16.1 | 单元测试覆盖率 | ≥ 80% | IEEE 829行业基准 |
| 15 | 16.2 | /HeartBeat P95响应 | ≤ 100ms | 高频调用低延迟要求 |
| 16 | 16.2 | /Function同步响应 | ≤ 200ms | 仅校验入队不执行 |
| 17 | 18.3 | 生物材料日志保存期 | ≥ 5年 | 《生物安全法》第38条 |
| 18 | 19.3 | AI置信度阈值 | ≥ 0.85 | F1 ≥ 0.85分类模型可靠性基准 |
| 19 | 20.1 | I级应急响应时限 | ≤ 30s | GB/T 24353-2009不可接受风险应对 |
| 20 | 20.3 | 急停指令响应时限 | ≤ 200ms | 同/Function同步响应上限 |
| 21 | 20.3 | 全平台急停覆盖 | ≤ 2s | 200驱动 × 10ms转发延迟 |
| 22 | 20.4 | 数据恢复RTO | ≤ 4h | 运维级数据恢复标准时限 |
| 23 | 20.4 | 数据恢复RPO | ≤ 24h | 日备份周期 |
| 24 | 23.2 | 开源依赖CVE阈值 | < 7.0 | CVSS v3.1高危分界线 |

---

*本标准由CIAI技术委员会制定并维护。标准中涉及的产品规范、技术管理规范和专项规范均基于实际工程实践与行业共识提炼而成，旨在为合成生物制造领域的设备集成提供统一、开放、安全、合规的技术框架。*

*标准分部分结构便于后续独立修订与扩展：第一部分（产品规范）、第二部分（技术与管理规范）、第三部分（专项规范）可各自独立进行版本迭代。*

*各章节均支持独立引用，引用格式示例："CIAI-STD-2.0 第7节"（接口定义）、"CIAI-STD-2.0 第18节"（生物安全规范）。*

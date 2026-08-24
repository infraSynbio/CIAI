# CIAI Device Integration Protocol 2.0

简体中文 | [English](protocol.en.md)

Status: public preview (`2.0.0-beta.1`)

本文件定义 CIAI 驱动与上位调度系统之间的语言无关线协议。C#、Java 或其他语言实现只要满足本文件和公开 Schema，即可作为兼容实现。

文中的“必须”“不得”“应该”“不应该”“可以”分别表示强制要求、禁止要求、推荐要求和可选能力。

## 1. 范围

CIAI 定义设备驱动的发现、健康检查、功能执行、同步操作、参数设置、状态读取以及样品/耗材进出。它不规定厂商设备内部报文，也不替代设备安全联锁、实验室生物安全制度或厂商操作规程。

## 2. 协议版本

- 当前主版本为 2。
- `2.x` 只能增加可选字段、可选枚举值或不改变现有行为的新能力。
- 删除字段、改变字段类型、改变端点方法或成功/失败语义必须升级主版本。
- CIAI 2.0 暂不提供线上版本协商；实现版本由发行版本和兼容性声明确定。
- `/Info` 中的 `basicInfo.version` 是设备驱动版本，不是协议协商字段。
- 接收方应该忽略未知 JSON 字段，但不得忽略已知字段的非法类型。

## 3. 传输

- 驱动必须提供 HTTP/1.1 或 HTTPS 服务。
- JSON 编码必须为 UTF-8。
- 有请求体的接口必须接受 `Content-Type: application/json`。
- 本机开发默认可以使用 HTTP；跨主机、生产或不可信网络部署应该使用 HTTPS，敏感控制环境应该使用 mTLS。
- 实现不得在日志中输出证书密码、Token、完整敏感样本信息或不必要的原始数据。

## 4. 固定端点

兼容实现必须只把下列 7 个路径作为 CIAI 2.0 标准端点：

| 方法 | 路径 | 请求 | 响应数据 |
|---|---|---|---|
| GET | `/Info` | 无 | `RegisterInfo` |
| GET | `/HeartBeat` | 无 | `HeartBeatInfo` |
| POST | `/Function` | `FunctionData` | 接受结果 |
| POST | `/Operation` | `OperationData` | 操作结果 |
| POST | `/Set` | `SetData[]` | boolean |
| GET | `/Get` | 无 | `GetReturn[]` |
| POST | `/EnterAndExit` | `EnterOrExitData` | `Finish` |

路径大小写敏感。已知路径使用错误 HTTP 方法必须返回 405；未知路径必须返回 404。`/FunctionSync` 不是 CIAI 2.0 端点。

## 5. 统一结果

除纯 HTTP 层拒绝外，响应使用：

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": {}
}
```

`code` 是机器可读业务码，`message` 是面向人的说明，`data` 是端点数据。已定义通用码：

| code | 含义 |
|---|---|
| `message.common.success` | 成功或请求已接受 |
| `message.common.failed` | 业务失败 |
| `message.common.unauthorized` | 未授权 |
| `message.common.timeout` | 超时 |
| `message.common.server.error` | 服务错误 |
| `message.common.parameters.missing` | 缺少参数 |

扩展错误码应该使用反向域名或项目命名空间，避免与公共码冲突。

## 6. HTTP 状态

| 状态 | 语义 |
|---|---|
| 200 | 请求格式有效；业务结果见 `Result.code` |
| 400 | JSON、必填字段或参数格式错误 |
| 404 | 非标准路径 |
| 405 | 标准路径使用了错误 HTTP 方法 |
| 413 | 请求体超过配置上限 |
| 429 | Function 队列或 HTTP 在途资源已满 |
| 500 | 未处理的宿主异常 |

业务失败不得伪装为 `message.common.success`。Set 返回裸 `false` 或成功包装的 `false` 时，宿主必须把该设置视为失败。

## 7. Function

`POST /Function` 接收长任务：

```json
{
  "functionName": "run",
  "functionParam": { "mode": "normal" },
  "instructionId": "instruction-001",
  "nestId": "robot_exchange",
  "labwareInfo": null
}
```

- `functionName` 必须对应 `/Info` 中声明的 Function。
- `instructionId` 应该在调度系统范围内唯一。
- 宿主必须快速返回“已接受”，不得在 HTTP 请求中阻塞到设备任务结束。
- 相同的非空 `instructionId` 必须幂等确认，不得重复执行。
- 队列已满必须返回 429。
- 启用回调时，完成结果必须保留原 `instructionId` 和 `nestId`。
- 成功终态必须由设备状态、厂商结果码或明确完成事件证明；Idle、Ready、Edit、连接成功或 void 返回不得单独证明成功。

完成数据：

```json
{
  "completion": "finish",
  "errorMsg": null,
  "instructionId": "instruction-001",
  "nestId": "robot_exchange",
  "resultOutput": [
    { "name": "temperatureCelsius", "resultData": 37.0 }
  ]
}
```

`completion` 至少支持 `finish` 和 `error`。错误完成应该提供 `errorMsg`。

## 8. Operation、Set、Get、EnterAndExit

- Operation 是同步控制动作，`operationName` 必须来自 `/Info`。
- Set 接受数组，驱动必须明确拒绝未知名称、非法类型和设备拒绝值。
- Get 返回所有公开状态；值通过 `getValue` 表达，调用方必须结合 `/Info` 中的类型和单位解释。
- CIAI 2.0 只发布一个 EnterAndExit 入口；具体进、出或转移动作由 `enterOrExitName` 分发。

## 9. Info 和声明式能力

`/Info` 必须完整发布：

- 设备名称、型号、厂商、驱动版本和设备类别；
- Function、Operation、Get、Set；
- Nest；
- EnterAndExit。

同一类别中的公开名称必须唯一。方法签名错误、重复名称和非法表单 JSON 必须在启动阶段失败，而不是在第一次请求时失败。

## 10. Nest

Nest 表示设备位置、通道、舱位或储存位置。实现不得根据名称猜测位置角色。以下信息必须来自设备文档、设备发现或部署者配置：

- 是否可被外部机械臂访问；
- 是否是来源或目的地；
- 是否为内部存储位、过渡位或外部交换位；
- 可接受耗材类型、坐标、姿态、列/层和高度。

固定位置可以由注解声明；由型号配置或设备发现产生的位置可以动态发布。

## 11. 心跳

心跳必须反映驱动的真实可用性，包括：

- 尚未初始化；
- 连接断开；
- 设备异常；
- 关键资源耗尽或忙碌；
- 正常可用。

DLL/API 驱动即使没有 SDK 通信对象，也必须根据厂商会话和初始化状态提供正确心跳。

`heartBeatTime` 必须是带 UTC 偏移量的 RFC 3339 时间戳。

## 12. 并发模型

下列概念彼此独立：

1. `FunctionalResources`：Function 业务资源数。
2. `Parallelizability`：上位系统看到的业务并行能力。
3. `deviceCallResources`：厂商 DLL/API 调用资源数。
4. `connections.*.maxConcurrency`：底层连接资源数。

TCP、串口和类似单通道请求–响应协议必须固定为 1，并保证一次发送及其响应不会被另一接口穿插。HTTP、process、DLL/API 只有在厂商明确支持多会话时才可以大于 1。

## 13. 扩展

- 设备专属配置放在 `device.settings`，由驱动转换为强类型配置。
- 多连接名称由驱动定义，SDK读取 `connections` 下的全部连接，但不会根据设备名称猜测使用哪一条。
- 非 TCP/HTTP/Serial/process/DLL 的传输应该通过 Provider 扩展，不得修改标准端点。
- 取消、进度和主动事件是当前 SDK 扩展能力，不构成 CIAI 2.0 的第八个 HTTP 端点。

## 14. 符合性

实现必须通过与语言无关的线协议测试，以及相应 SDK 的契约测试。符合性证据至少包括：

- 7 个端点、HTTP 方法和 JSON 字段；
- 400/404/405/413/429/500；
- Function 接受、幂等、队列和回调；
- 注册、心跳、Nest 和返回值语义；
- TCP/串口精确字节、帧边界和并发事务；
- 配置诊断、启动失败和优雅停机。

完整清单见 [conformance.md](conformance.md)。OpenAPI 和 JSON Schema 是机器可读辅助材料；若其与本文件冲突，以本文件和契约测试的共同可观察行为为准，并应提交 Issue 修正文档差异。

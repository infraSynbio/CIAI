# CIAI2Controller SDK API 接口文档

## 概述

CIAI2Controller SDK 基于 **CIAI 2.0 驱动侧集成标准规范**，提供 7 个标准 REST API 接口供上层调度系统调用。所有接口使用 **JSON** 格式通信，响应统一封装为 `Result<T>` 结构。

### 通用响应格式 (Result\<T\>)

```json
{
  "code": "message.common.success",
  "message": "Success",
  "data": { ... }
}
```

### 通用状态码

| 状态码 | 说明 |
|---|---|
| `message.common.success` | 成功 |
| `message.common.failed` | 失败 |
| `message.common.unauthorized` | 未授权 |
| `message.common.timeout` | 超时 |
| `message.common.server.error` | 服务器错误 |
| `message.common.parameters.missing` | 参数缺失 |

---

## 接口列表

### 1. /Info — 获取设备注册信息

获取设备驱动的基本信息、功能列表、参数配置、位置信息等元数据。

- **URL**: `GET /Info`
- **请求参数**: 无

**响应示例** (`Result<RegisterInfo>`):

```json
{
  "code": "message.common.success",
  "data": {
    "basicInfo": {
      "equipmentName": "WaspSealer",
      "equipmentNameEN": "Sealer",
      "equipmentModel": "Wasp",
      "equipmentManufacturer": "KBIOSYSTEMS",
      "version": "1.0.0",
      "author": "刘荣",
      "equipmentClass": "封膜机",
      "equipmentType": 1,
      "functionalResources": 1,
      "canE_Stop": 0,
      "runtimeAccessibility": 0,
      "parallelizability": 0,
      "equipmentIcon": "data:image/png;base64,..."
    },
    "advancedInfo": {
      "equipmentFunctions": [
        {
          "functionName": "seal_film",
          "functionTitleCN": "封膜",
          "functionTitleEN": "Seal",
          "functionDescription": "对微孔板进行热封膜",
          "functionDefaultPeriod": "60",
          "functionCategoryCN": "封膜",
          "functionCategoryEN": "Sealing",
          "iconBlack": "base64...",
          "iconWhite": "base64...",
          "functionFormJsonStructure": "{...}"
        }
      ],
      "equipmentGetInfos": [
        {
          "getName": "current_temperature",
          "getTitleCN": "当前温度",
          "getTitleEN": "Current Temperature",
          "getType": "double",
          "getUnit": "°C",
          "getDescription": "当前加热板温度"
        }
      ],
      "equipmentSetInfos": [
        {
          "setName": "target_temperature",
          "setTitleCN": "目标温度",
          "setTitleEN": "Target Temperature",
          "setType": "double",
          "setValue": ["25.0", "37.0", "42.0"],
          "setUnit": "°C",
          "setDescription": "设置目标加热温度"
        }
      ],
      "equipmentNests": [
        {
          "nestName": "Wasp1_1",
          "labwareType": "96-well plate",
          "nestPostures": "horizontal",
          "nestHeight": 0.0,
          "nestCoordinate": "0,0,0",
          "nestDescription": "封膜工位"
        }
      ],
      "equipmentOperations": [
        {
          "operationName": "reset",
          "operationTitleCN": "复位",
          "operationTitleEN": "Reset",
          "operationDescription": "设备复位"
        }
      ],
      "equipmentEnterAndExit": {
        "enterAndExitName": "load_sample",
        "enterAndExitTitleCN": "装载/卸载",
        "enterAndExitTitleEN": "Load/Unload"
      }
    }
  }
}
```

**basicInfo 字段说明**:

| 字段 | 类型 | 说明 |
|---|---|---|
| equipmentName | string | 设备名称 |
| equipmentNameEN | string | 设备英文名称 |
| equipmentModel | string | 设备型号 |
| equipmentManufacturer | string | 制造商 |
| version | string | 驱动版本 |
| author | string | 驱动作者 |
| equipmentClass | string | 设备分类（如：封膜机、离心机） |
| equipmentType | int | 1-核心设备 2-转移设备 3-辅助设备 4-存储设备 |
| functionalResources | int | 可并发执行的功能数 |
| canE_Stop | int | 是否支持急停 0-否 1-是 |
| runtimeAccessibility | int | 运行时可访问性 |
| parallelizability | int | 是否可并行 |
| equipmentIcon | string | 设备图标（Base64编码） |

---

### 2. /HeartBeat — 心跳检测

获取设备驱动的健康状态。

- **URL**: `GET /HeartBeat`
- **请求参数**: 无

**响应示例** (`Result<HeartBeatInfo>`):

```json
{
  "code": "message.common.success",
  "data": {
    "heartBeatStatus": 0,
    "heartBeatTime": "2026-06-26T12:00:00"
  }
}
```

**heartBeatStatus 状态码**:

| 值 | 枚举 | 说明 |
|---|---|---|
| 0 | Normal | 正常 |
| 1 | DriverAbnormal | 驱动异常 |
| 2 | DriverOverTime | 驱动超时 |
| 3 | EquipmentAbnormal | 设备异常 |
| 4 | EquipmentError | 设备错误 |
| 5 | EquipmentOverTime | 设备超时 |
| 6 | Monitoring | 监控中 |

---

### 3. /Function — 执行功能（异步+回调）

执行设备功能，**立即返回受理结果**，功能在后台执行，完成后通过 HTTP POST 回调通知结果。

- **URL**: `POST /Function`
- **Content-Type**: `application/json`

**请求体** (`FunctionData`):

```json
{
  "functionName": "seal_film",
  "instructionId": "instr-001",
  "labwareInfo": {
    "LabwareName": "96-well plate",
    "capacity": "200uL",
    "capacityRow": 8,
    "capacityColumn": 12
  },
  "equipmentName": "WaspSealer",
  "nestId": "Wasp1_1",
  "userId": "user-001",
  "taskId": "task-001",
  "functionParam": {
    "sealingTemperature": 165,
    "sealingTime": 2.5
  }
}
```

**请求字段说明**:

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| functionName | string | 是 | 功能名称，须与 /Info 返回的 functionName 一致 |
| instructionId | string | 是 | 指令ID，用于回调时关联 |
| labwareInfo | object | 否 | 耗材信息 |
| labwareInfo.LabwareName | string | 否 | 耗材名称，如 "96-well plate" |
| labwareInfo.capacity | string | 否 | 容量 |
| labwareInfo.capacityRow | int | 否 | 行数 |
| labwareInfo.capacityColumn | int | 否 | 列数 |
| equipmentName | string | 否 | 设备名称 |
| nestId | string | 否 | 操作位置ID |
| userId | string | 否 | 用户ID |
| taskId | string | 否 | 任务ID |
| functionParam | object | 否 | 功能参数（根据 functionFormJsonStructure 定义） |

**立即响应** (`Result<string>`):

```json
{
  "code": "message.common.success",
  "message": "Function accepted",
  "data": "Function accepted"
}
```

**异步回调**（SDK 向配置的 `callback.url` 发送 POST）:

```json
{
  "completion": "finish",
  "instructionId": "instr-001",
  "nestId": "Wasp1_1",
  "resultOutput": [
    { "name": "finalTemperature", "resultData": "37.0" }
  ]
}
```

**回调字段说明**:

| 字段 | 类型 | 说明 |
|---|---|---|
| completion | string | `finish` 完成 / `error` 失败 |
| instructionId | string | 与请求中的 instructionId 一致 |
| nestId | string | 操作位置ID |
| errorMsg | string | 错误信息（completion=error 时） |
| resultOutput | array | 输出结果列表 |
| resultOutput[].name | string | 输出项名称 |
| resultOutput[].resultData | object | 输出值 |

---

### 4. /Operation — 执行操作（同步）

执行设备操作，**阻塞等待完成后直接返回结果**。

- **URL**: `POST /Operation`
- **Content-Type**: `application/json`

**请求体** (`OperationData`):

```json
{
  "operationName": "reset",
  "operationParam": null
}
```

**请求字段说明**:

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| operationName | string | 是 | 操作名称，须与 /Info 返回的 operationName 一致 |
| operationParam | object | 否 | 操作参数 |

**响应** (`Result<bool>`):

```json
{
  "code": "message.common.success",
  "data": true
}
```

---

### 5. /Set — 设置参数

设置设备参数。

- **URL**: `POST /Set`
- **Content-Type**: `application/json`

**请求体** (`SetData[]`):

```json
[
  {
    "setName": "target_temperature",
    "setValue": "37.0"
  }
]
```

**请求字段说明**:

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| setName | string | 是 | 参数名称，须与 /Info 返回的 setName 一致 |
| setValue | string | 是 | 参数值（统一用字符串传递） |

**响应** (`Result<bool>`):

```json
{
  "code": "message.common.success",
  "data": true
}
```

---

### 6. /Get — 获取状态

获取设备当前状态信息。

- **URL**: `GET /Get`
- **请求参数**: 无

**响应** (`Result<List<GetReturn>>`):

```json
{
  "code": "message.common.success",
  "data": [
    { "getName": "current_temperature", "getValue": "25.0" },
    { "getName": "is_heating", "getValue": "false" },
    { "getName": "is_running", "getValue": "true" }
  ]
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| getName | string | 状态名称 |
| getValue | string | 状态值（统一用字符串返回） |

---

### 7. /EnterAndExit — 装载/卸载

执行样本板的装载或卸载操作（同步）。

- **URL**: `POST /EnterAndExit`
- **Content-Type**: `application/json`

**请求体** (`EnterOrExitData`):

```json
{
  "enterOrExitName": "load_sample",
  "enterOrExitValue": null
}
```

**请求字段说明**:

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| enterOrExitName | string | 是 | 进出操作名称 |
| enterOrExitValue | object | 否 | 进出操作参数 |

**响应** (`Result<Finish>`):

```json
{
  "code": "message.common.success",
  "data": {
    "completion": "finish"
  }
}
```

---

## 调用流程示例

### 典型调用时序

```
调度系统                        CIAI2驱动
   |                               |
   |── GET /Info ─────────────────>|  查询设备能力和参数
   |<── 设备元数据 ────────────────|
   |                               |
   |── GET /HeartBeat ────────────>|  检查设备健康状态
   |<── 心跳状态 ─────────────────|
   |                               |
   |── POST /Set ─────────────────>|  设置运行参数
   |<── 设置结果 ─────────────────|
   |                               |
   |── POST /Function ────────────>|  下发功能指令（异步）
   |<── 立即受理 ─────────────────|
   |                               |  功能执行中...
   |                               |
   |<── POST callback ────────────|  执行完成回调
   |── 200 OK ───────────────────>|
   |                               |
   |── GET /Get ──────────────────>|  查询最终状态
   |<── 当前状态 ─────────────────|
```

### 完整调用示例

以下示例假设驱动部署在 `192.168.1.100:8443`，使用 HTTPS。

```bash
# 1. 获取设备信息
curl --cacert ca.pem https://192.168.1.100:8443/Info

# 2. 心跳检测
curl --cacert ca.pem https://192.168.1.100:8443/HeartBeat

# 3. 设置温度参数
curl --cacert ca.pem -X POST https://192.168.1.100:8443/Set \
  -H "Content-Type: application/json" \
  -d '[{"setName":"target_temperature","setValue":"37.0"}]'

# 4. 执行加热功能（异步）
curl --cacert ca.pem -X POST https://192.168.1.100:8443/Function \
  -H "Content-Type: application/json" \
  -d '{
    "functionName": "heat",
    "instructionId": "instr-20260626-001",
    "nestId": "Incubator1_1",
    "functionParam": {"targetTemperature": 37.0, "duration": 300}
  }'

# 5. 查询状态
curl --cacert ca.pem https://192.168.1.100:8443/Get

# 6. 执行复位操作（同步）
curl --cacert ca.pem -X POST https://192.168.1.100:8443/Operation \
  -H "Content-Type: application/json" \
  -d '{"operationName":"reset","operationParam":null}'
```

---

## 通信安全

SDK 支持以下通信安全模式：

### HTTPS (单向认证)
服务端配置 TLS 证书，客户端验证服务端身份：
```yaml
server:
  port: 8443
  useHttps: true
  certificate:
    path: "./certs/server.pfx"
    password: "${CIAI_SERVER_CERT_PASSWORD}"
```

### HTTPS + mTLS (双向认证)
同时验证服务端和客户端证书：
```yaml
server:
  clientAuth:
    mode: "need"
    enabled: true
    trustedThumbprints:
      - "ABCD1234567890..."
```

客户端调用时需携带客户端证书。如使用 curl：
```bash
curl --cert client.pem --key client.key \
  --cacert ca.pem \
  https://192.168.1.100:8443/Info
```

### HTTP (无加密，仅开发/内网环境)
```yaml
server:
  useHttps: false
  port: 8080
```

---

## 错误处理

请求格式有效但设备业务执行失败时通常返回 HTTP 200，并通过响应体中的 `code` 字段判断：

```json
{
  "code": "message.common.failed",
  "message": "设备通信超时"
}
```

HTTP层拒绝使用对应状态码：非法JSON或缺少参数为400、未知路径为404、错误方法为405、请求体过大为413、资源/队列满为429、未处理宿主异常为500。

常见错误码：

| code | 说明 |
|---|---|
| `message.common.unauthorized` | 客户端证书未授权 |
| `message.common.timeout` | 操作超时 |
| `message.common.server.error` | 服务器内部错误 |
| `message.common.parameters.missing` | 缺少必要参数 |

# Conformance

CIAI 的兼容性以公开线协议和可观察行为为准，而不是以类名或 README 相似度判断。

## SDK 契约测试

.NET：

```powershell
dotnet run --project CiaiControllerSDK.ContractTests/CiaiControllerSDK.ContractTests.csproj -c Release
```

Java：

```bash
./mvnw --batch-mode --no-transfer-progress verify
```

两套测试覆盖注解注册、异步返回、错误结果、强类型参数、命名连接、资源限制、TCP帧、Function幂等、429、HTTP方法、取消/进度、动态 Nest、无硬件配置预检、配置相对路径和发布样例校验；Java还覆盖信号量所有权与许可不膨胀。

## 驱动符合性等级

| 等级 | 要求 |
|---|---|
| Core | 7 个端点、统一 Result、注册、心跳和方法状态码 |
| Runtime | Function异步接受、回调、幂等、背压、资源和优雅停机 |
| Transport | 对实际TCP/串口/HTTP/DLL/process行为完成协议测试 |
| Hardware | 使用真实设备完成状态机、错误、安全联锁和恢复验证 |

模拟测试只能证明 Core/Runtime 行为，不能代替 Transport 或 Hardware 验证。

## 新驱动最小证据

- 设备文档到 Function/Operation/Get/Set/Nest/EnterExit 的逐项映射；
- 初始化、断开、急停、复位、状态和完成条件；
- 原始字节、编码、帧边界、校验、超时和重连；
- Nest 的机械臂可达性、存储/过渡角色、坐标和姿态来源；
- 正常成功、明确失败、无成功证据却回到空闲、暂停/恢复、终止/取消；
- 厂商 SDK 位数、运行时、原生依赖和部署许可；
- 未验证硬件或环境的明确清单。

更详细的审计表见 [conformance-checklist.md](conformance-checklist.md)。
使用AI从设备文档或旧驱动开始迁移时，遵循
[device-driver-migration Skill](../.agents/skills/device-driver-migration/SKILL.md) 的证据分级、
状态成功条件和分层验收流程。

## 黑盒检查

第三方实现至少应验证：

1. 对 7 个端点执行正确请求；
2. 对已知端点发送错误方法并得到 405；
3. 请求未知路径并得到 404；
4. 发送非法 JSON、空名称和超大请求体；
5. 重复提交相同 `instructionId`，确认设备只执行一次；
6. 填满 Function 队列，确认返回 429；
7. 核对 `/Info` 与真实实现数量和字段；
8. 模拟断连和资源耗尽，确认心跳不会永久为 Normal；
9. 验证关闭过程有界且不会接受新任务。

计划提供独立的 `ciai-conformance` 黑盒 CLI；在 CLI 稳定前，以本仓库契约测试和上述检查为准。

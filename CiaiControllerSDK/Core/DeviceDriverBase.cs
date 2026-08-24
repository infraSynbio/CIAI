using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Attributes;
using CiaiControllerSDK.Icons;
using CiaiControllerSDK.Interfaces;
using CiaiControllerSDK.Logging;
using CiaiControllerSDK.Models;
using CiaiControllerSDK.Services;
using CiaiControllerSDK.Config;

namespace CiaiControllerSDK.Core
{
    /// <summary>
    /// 设备驱动基类 - 用户继承此类实现具体设备驱动
    /// </summary>
    public abstract class DeviceDriverBase : IAsyncDisposable
    {
        private readonly SemaphoreSlim _resourceSemaphore;
        private SemaphoreSlim _deviceCallSemaphore;
        private TimeSpan _deviceCallTimeout;
        private ICommunication _communication;
        private ConnectionManager _connectionManager;
        private bool _disposed;
        private readonly Dictionary<string, MethodInfo> _functionMethods = new();
        private readonly Dictionary<string, MethodInfo> _operationMethods = new();
        private readonly Dictionary<string, MethodInfo> _setMethods = new();
        private readonly Dictionary<string, MethodInfo> _getMethods = new();
        private readonly Dictionary<string, MethodInfo> _enterExitMethods = new();
        private readonly List<PropertyInfo> _nestProperties = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _instructionCancellations = new();
        private readonly AsyncLocal<DriverExecutionContext> _executionContext = new();

        /// <summary>进度、报警和设备主动上报的统一事件。</summary>
        public event EventHandler<DriverEvent> EventPublished;

        protected DriverExecutionContext CurrentExecution => _executionContext.Value;
        protected CancellationToken ExecutionCancellationToken =>
            CurrentExecution?.CancellationToken ?? CancellationToken.None;

        public bool CancelInstruction(string instructionId)
        {
            if (string.IsNullOrWhiteSpace(instructionId)) return false;
            if (!_instructionCancellations.TryGetValue(instructionId, out var source)) return false;
            try { source.Cancel(); }
            catch (ObjectDisposedException) { return false; }
            return true;
        }

        protected void ReportProgress(double progress, string message = null, object data = null)
        {
            if (progress < 0 || progress > 100) throw new ArgumentOutOfRangeException(nameof(progress));
            PublishEvent("progress", message, data, progress);
        }

        protected void PublishEvent(string type, string message = null, object data = null,
            double? progress = null)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("事件类型不能为空", nameof(type));
            var driverEvent = new DriverEvent
            {
                Type = type,
                InstructionId = CurrentExecution?.InstructionId,
                NestId = CurrentExecution?.NestId,
                Progress = progress,
                Message = message,
                Data = data
            };
            var handlers = EventPublished;
            if (handlers == null) return;
            foreach (EventHandler<DriverEvent> handler in handlers.GetInvocationList())
            {
                try { handler(this, driverEvent); }
                catch (Exception ex) { Logger.LogWarning(ex, "驱动事件订阅者处理失败: {EventType}", type); }
            }
        }

        /// <summary>
        /// 设备配置
        /// </summary>
        public DeviceConfiguration Configuration { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; protected set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _connectionManager?.IsConnected ?? (_communication?.IsConnected ?? false);

        /// <summary>
        /// 底层设备调用资源总数。与FunctionalResources和Parallelizability相互独立。
        /// </summary>
        public int DeviceCallResources => Configuration.DeviceCallResources;

        /// <summary>
        /// 设备驱动属性信息
        /// </summary>
        public DeviceDriverAttribute DriverAttribute { get; }

        /// <summary>
        /// 日志器
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// 当前通信对象，供需要类型特定能力（如按帧读取）的驱动使用。
        /// </summary>
        protected ICommunication Communication => _communication;

        /// <summary>按名称获取声明式连接；不传名称时返回默认连接。</summary>
        protected ICommunication GetCommunication(string name = null) =>
            _connectionManager?.Get(name) ??
            (string.IsNullOrWhiteSpace(name) || string.Equals(name, "default", StringComparison.OrdinalIgnoreCase)
                ? _communication
                : throw new KeyNotFoundException($"未配置连接: {name}"));

        /// <summary>
        /// 在连接自己的资源信号量内执行调用。TCP/串口固定串行，HTTP/DLL/API遵循maxConcurrency。
        /// </summary>
        protected Task<T> ExecuteConnectionCallAsync<T>(string name,
            Func<ICommunication, Task<T>> action, CancellationToken cancellationToken = default) =>
            _connectionManager != null
                ? _connectionManager.ExecuteAsync(name, action, cancellationToken)
                : ExecuteDeviceCallAsync(() => action(GetCommunication(name)), cancellationToken);

        protected Task ExecuteConnectionCallAsync(string name, Func<ICommunication, Task> action,
            CancellationToken cancellationToken = default) =>
            _connectionManager != null
                ? _connectionManager.ExecuteAsync(name, action, cancellationToken)
                : ExecuteDeviceCallAsync(() => action(GetCommunication(name)), cancellationToken);

        /// <summary>
        /// 声明式驱动使用的无参构造函数。DriverHost会在初始化前自动注入YAML设备配置。
        /// </summary>
        protected DeviceDriverBase() : this(new DeviceConfiguration())
        {
        }

        protected DeviceDriverBase(DeviceConfiguration configuration)
        {
            // 获取设备驱动属性
            DriverAttribute = GetType().GetCustomAttribute<DeviceDriverAttribute>()
                ?? throw new InvalidOperationException($"类型 {GetType().Name} 必须标记 [DeviceDriver] 属性");

            // 初始化日志器
            Logger = LoggerProvider.CreateLogger(GetType().Name);

            // 初始化资源信号量
            if (DriverAttribute.FunctionalResources <= 0)
                throw new InvalidOperationException("FunctionalResources 必须大于0");
            _resourceSemaphore = new SemaphoreSlim(DriverAttribute.FunctionalResources, DriverAttribute.FunctionalResources);
            ApplyConfiguration(configuration ?? throw new ArgumentNullException(nameof(configuration)));

            // 扫描并注册方法
            ScanAndRegisterMethods();
        }

        /// <summary>
        /// 由DriverHost在初始化前注入配置。普通驱动无需调用。
        /// </summary>
        internal void ApplyConfiguration(DeviceConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (IsInitialized)
                throw new InvalidOperationException("设备初始化后不能重新应用配置");
            if (configuration.DeviceCallResources <= 0)
                throw new InvalidOperationException("DeviceCallResources 必须大于0");
            if (configuration.DeviceCallTimeout <= 0)
                throw new InvalidOperationException("DeviceCallTimeout 必须大于0");

            _deviceCallSemaphore?.Dispose();
            _deviceCallSemaphore = new SemaphoreSlim(
                configuration.DeviceCallResources, configuration.DeviceCallResources);
            _deviceCallTimeout = TimeSpan.FromMilliseconds(configuration.DeviceCallTimeout);
            Configuration = configuration;
        }

        /// <summary>
        /// 扫描并注册标记的方法
        /// </summary>
        private void ScanAndRegisterMethods()
        {
            var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                // 功能方法
                var functionAttr = method.GetCustomAttribute<DeviceFunctionAttribute>();
                if (functionAttr != null)
                {
                    ValidateMethodParameters(method, nameof(DeviceFunctionAttribute), typeof(FunctionData));
                    ValidateFormJson(method, functionAttr.FormJson, nameof(DeviceFunctionAttribute));
                    RegisterUniqueMethod(_functionMethods, functionAttr.Name, method, nameof(DeviceFunctionAttribute));
                }

                // 操作方法
                var operationAttr = method.GetCustomAttribute<DeviceOperationAttribute>();
                if (operationAttr != null)
                {
                    ValidateMethodParameters(method, nameof(DeviceOperationAttribute), typeof(OperationData));
                    ValidateFormJson(method, operationAttr.FormJson, nameof(DeviceOperationAttribute));
                    RegisterUniqueMethod(_operationMethods, operationAttr.Name, method, nameof(DeviceOperationAttribute));
                }

                // 设置方法
                var setAttr = method.GetCustomAttribute<DeviceSetAttribute>();
                if (setAttr != null)
                {
                    ValidateSetMethod(method);
                    RegisterUniqueMethod(_setMethods, setAttr.Name, method, nameof(DeviceSetAttribute));
                }

                // 获取方法
                var getAttr = method.GetCustomAttribute<DeviceGetAttribute>();
                if (getAttr != null)
                {
                    ValidateMethodParameters(method, nameof(DeviceGetAttribute));
                    RegisterUniqueMethod(_getMethods, getAttr.Name, method, nameof(DeviceGetAttribute));
                }

                // 进出方法
                var enterExitAttr = method.GetCustomAttribute<DeviceEnterExitAttribute>();
                if (enterExitAttr != null)
                {
                    ValidateMethodParameters(method, nameof(DeviceEnterExitAttribute), typeof(EnterOrExitData));
                    RegisterUniqueMethod(_enterExitMethods, enterExitAttr.Name, method, nameof(DeviceEnterExitAttribute));
                }
            }

            // 扫描位置属性
            var properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var property in properties)
            {
                var nestAttr = property.GetCustomAttribute<DeviceNestAttribute>();
                if (nestAttr != null)
                {
                    if (property.PropertyType != typeof(EquipmentNest) || property.GetMethod == null)
                    {
                        throw new InvalidOperationException(
                            $"{property.DeclaringType?.Name}.{property.Name} 的DeviceNest必须是可读的EquipmentNest属性");
                    }
                    _nestProperties.Add(property);
                }
            }

            if (_enterExitMethods.Count > 1)
                throw new InvalidOperationException(
                    "注册协议只能发布一个DeviceEnterExit；请用一个入口方法根据参数分发进/出动作");

            // 按 Order 排序
            _nestProperties.Sort((a, b) =>
            {
                var orderA = a.GetCustomAttribute<DeviceNestAttribute>()?.Order ?? 0;
                var orderB = b.GetCustomAttribute<DeviceNestAttribute>()?.Order ?? 0;
                return orderA.CompareTo(orderB);
            });
        }

        private static void RegisterUniqueMethod(Dictionary<string, MethodInfo> methods, string name,
            MethodInfo method, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"{method.DeclaringType?.Name}.{method.Name} 的 {attributeName} name不能为空");

            if (methods.TryGetValue(name, out var existing))
            {
                throw new InvalidOperationException(
                    $"{attributeName} name重复: {name} ({existing.Name}, {method.Name})");
            }

            methods.Add(name, method);
        }

        private static void ValidateMethodParameters(MethodInfo method, string attributeName,
            params Type[] expectedTypes)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != expectedTypes.Length ||
                parameters.Where((parameter, index) =>
                    !parameter.ParameterType.IsAssignableFrom(expectedTypes[index])).Any())
            {
                var expected = expectedTypes.Length == 0
                    ? "无参数"
                    : string.Join(", ", expectedTypes.Select(type => type.Name));
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name} 的{attributeName}参数必须为: {expected}");
            }
        }

        private static void ValidateSetMethod(MethodInfo method)
        {
            if (method.GetParameters().Length != 1)
            {
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name} 的DeviceSet必须且只能有一个参数");
            }
        }

        private static void ValidateFormJson(MethodInfo method, string formJson, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(formJson))
                return;
            try
            {
                using var _ = JsonDocument.Parse(formJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name} 的{attributeName}.FormJson不是有效JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置通信接口
        /// </summary>
        protected void SetCommunication(ICommunication communication)
        {
            if (communication == null)
                throw new ArgumentNullException(nameof(communication));
            if (ReferenceEquals(_communication, communication))
                return;

            if (_communication is IDisposable disposable)
                disposable.Dispose();
            _communication = communication;
        }

        /// <summary>
        /// 根据DeviceConfiguration创建并设置SDK内置通信对象。
        /// DLL类型返回null。
        /// </summary>
        protected ICommunication UseConfiguredCommunication()
        {
            var communication = CommunicationFactory.Create(Configuration);
            if (communication != null)
                SetCommunication(communication);
            return communication;
        }

        /// <summary>
        /// 初始化设备
        /// </summary>
        public virtual async Task<bool> InitializeAsync()
        {
            if (IsInitialized)
                return true;

            try
            {
                ConfigurationValidator.ValidateAndThrow(Configuration);
                if (Configuration.Connections?.Count > 0)
                {
                    _connectionManager = new ConnectionManager(Configuration.Connections.Values);
                    _communication = _connectionManager.Default;
                    if (!await _connectionManager.ConnectAsync())
                    {
                        await _connectionManager.DisposeAsync();
                        _connectionManager = null;
                        _communication = null;
                        return false;
                    }
                    IsInitialized = true;
                    return true;
                }

                if (_communication == null && Configuration.CommunicationType != CommunicationType.DLL)
                {
                    if (!CommunicationFactory.CanCreate(Configuration))
                    {
                        Logger.LogError("通信配置不完整: {CommunicationType}", Configuration.CommunicationType);
                        return false;
                    }

                    _communication = CommunicationFactory.Create(Configuration);
                }

                // DLL/COM型驱动通常不使用SDK的ICommunication实现，允许其通过
                // 重写InitializeAsync或在外部封装层完成连接。
                if (_communication != null && !await _communication.ConnectAsync())
                    return false;

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "设备初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 初始化设备（同步版本）
        /// </summary>
        public virtual bool Initialize()
        {
            return InitializeAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        public virtual async Task DisconnectAsync()
        {
            try
            {
                if (_communication != null)
                {
                    if (_connectionManager != null)
                        await _connectionManager.DisconnectAsync();
                    else
                        await _communication.DisconnectAsync();
                }
            }
            finally
            {
                IsInitialized = false;
            }
        }

        /// <summary>
        /// 断开设备连接（同步版本）
        /// </summary>
        public virtual void Disconnect()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }

        #region 资源管理

        /// <summary>
        /// 获取资源（阻塞）
        /// </summary>
        protected async Task<bool> AcquireResourceAsync(CancellationToken cancellationToken = default)
        {
            return await AcquireResourceAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }

        /// <summary>
        /// 在指定时间内获取功能资源。
        /// </summary>
        protected async Task<bool> AcquireResourceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _resourceSemaphore.WaitAsync(timeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// 同步获取功能资源，默认等待30秒。
        /// </summary>
        protected bool AcquireResource(TimeSpan? timeout = null)
        {
            return _resourceSemaphore.Wait(timeout ?? TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 尝试获取资源（非阻塞）
        /// </summary>
        protected bool TryAcquireResource()
        {
            return _resourceSemaphore.Wait(0);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected void ReleaseResource()
        {
            _resourceSemaphore.Release();
        }

        /// <summary>
        /// 在独立的底层设备调用资源内执行厂商DLL/API操作。
        /// 只包住真实设备调用，不要包住整个长时间业务Function。
        /// </summary>
        protected async Task<T> ExecuteDeviceCallAsync<T>(Func<Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (!await _deviceCallSemaphore.WaitAsync(_deviceCallTimeout, cancellationToken))
                throw new TimeoutException("等待底层设备调用资源超时");

            try
            {
                return await action();
            }
            finally
            {
                _deviceCallSemaphore.Release();
            }
        }

        protected async Task ExecuteDeviceCallAsync(Func<Task> action,
            CancellationToken cancellationToken = default)
        {
            await ExecuteDeviceCallAsync(async () =>
            {
                await action();
                return true;
            }, cancellationToken);
        }

        protected T ExecuteDeviceCall<T>(Func<T> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (!_deviceCallSemaphore.Wait(_deviceCallTimeout))
                throw new TimeoutException("等待底层设备调用资源超时");

            try
            {
                return action();
            }
            finally
            {
                _deviceCallSemaphore.Release();
            }
        }

        protected void ExecuteDeviceCall(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            ExecuteDeviceCall(() =>
            {
                action();
                return true;
            });
        }

        #endregion

        #region 功能接口

        /// <summary>
        /// 执行功能
        /// </summary>
        public virtual async Task<Result<Finish>> ExecuteFunctionAsync(FunctionData data)
        {
            var startTime = DateTime.Now;
            Logger.LogInformation("开始执行功能: {FunctionName}, 指令ID: {InstructionId}",
                data?.FunctionName, data?.InstructionId);

            if (!IsInitialized)
            {
                Logger.LogWarning("执行功能失败: 设备未初始化");
                return Result<Finish>.Failed("设备未初始化");
            }

            if (string.IsNullOrEmpty(data?.FunctionName))
            {
                Logger.LogWarning("执行功能失败: 功能名称不能为空");
                return Result<Finish>.Failed("功能名称不能为空");
            }

            if (!_functionMethods.TryGetValue(data.FunctionName, out var method))
            {
                Logger.LogWarning("执行功能失败: 未找到功能 {FunctionName}", data.FunctionName);
                return Result<Finish>.Failed($"未找到功能: {data.FunctionName}");
            }

            // 获取资源
            if (!await AcquireResourceAsync())
            {
                Logger.LogWarning("执行功能失败: 获取资源超时, 功能: {FunctionName}", data.FunctionName);
                return Result<Finish>.Failed("获取资源超时");
            }

            try
            {
                using var cancellation = new CancellationTokenSource();
                if (!string.IsNullOrWhiteSpace(data.InstructionId))
                    _instructionCancellations[data.InstructionId] = cancellation;
                _executionContext.Value = new DriverExecutionContext(
                    data.InstructionId, data.NestId, cancellation.Token);
                Logger.LogDebug("调用功能方法: {FunctionName}", data.FunctionName);

                var invocationResult = method.Invoke(this, new object[] { data });
                var value = await AwaitInvocationResultAsync(invocationResult);
                var result = value switch
                {
                    Result<Finish> sdkResult => sdkResult,
                    Finish finish => Result<Finish>.Success(finish),
                    _ => Result<Finish>.Success(Finish.Success())
                };

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.LogInformation("功能执行完成: {FunctionName}, 结果: {Completion}, 耗时: {Elapsed}ms",
                    data.FunctionName, result.Data?.Completion, elapsed);
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("功能已取消: {FunctionName}, 指令ID: {InstructionId}",
                    data.FunctionName, data.InstructionId);
                return Result<Finish>.Failed("任务已取消");
            }
            catch (Exception ex)
            {
                ex = UnwrapInvocationException(ex);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.LogError(ex, "执行功能异常: {FunctionName}, 耗时: {Elapsed}ms", data.FunctionName, elapsed);
                return Result<Finish>.Failed($"执行功能失败: {ex.Message}");
            }
            finally
            {
                _executionContext.Value = null;
                if (!string.IsNullOrWhiteSpace(data.InstructionId))
                    _instructionCancellations.TryRemove(data.InstructionId, out _);
                ReleaseResource();
            }
        }

        /// <summary>
        /// 执行功能（同步版本）
        /// </summary>
        public virtual Result<Finish> ExecuteFunction(FunctionData data)
        {
            return ExecuteFunctionAsync(data).GetAwaiter().GetResult();
        }

        #endregion

        #region 操作接口

        /// <summary>
        /// 执行操作
        /// </summary>
        public virtual async Task<Result<bool>> ExecuteOperationAsync(OperationData data)
        {
            var startTime = DateTime.Now;
            Logger.LogInformation("开始执行操作: {OperationName}", data?.OperationName);

            if (!IsInitialized)
            {
                Logger.LogWarning("执行操作失败: 设备未初始化");
                return Result<bool>.Failed("设备未初始化");
            }

            if (string.IsNullOrEmpty(data?.OperationName))
            {
                Logger.LogWarning("执行操作失败: 操作名称不能为空");
                return Result<bool>.Failed("操作名称不能为空");
            }

            if (!_operationMethods.TryGetValue(data.OperationName, out var method))
            {
                Logger.LogWarning("执行操作失败: 未找到操作 {OperationName}", data.OperationName);
                return Result<bool>.Failed($"未找到操作: {data.OperationName}");
            }

            try
            {
                Logger.LogDebug("调用操作方法: {OperationName}", data.OperationName);

                var invocationResult = method.Invoke(this, new object[] { data });
                var value = await AwaitInvocationResultAsync(invocationResult);
                var result = value switch
                {
                    Result<bool> sdkResult => sdkResult,
                    bool success => Result<bool>.Success(success),
                    _ => Result<bool>.Success(true)
                };

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.LogInformation("操作执行完成: {OperationName}, 结果: {Result}, 耗时: {Elapsed}ms",
                    data.OperationName, result.Data, elapsed);
                return result;
            }
            catch (Exception ex)
            {
                ex = UnwrapInvocationException(ex);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.LogError(ex, "执行操作异常: {OperationName}, 耗时: {Elapsed}ms", data.OperationName, elapsed);
                return Result<bool>.Failed($"执行操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行操作（同步版本）
        /// </summary>
        public virtual Result<bool> ExecuteOperation(OperationData data)
        {
            return ExecuteOperationAsync(data).GetAwaiter().GetResult();
        }

        #endregion

        #region 设置接口

        /// <summary>
        /// 执行设置
        /// </summary>
        public virtual async Task<Result<bool>> ExecuteSetAsync(List<SetData> setDataList)
        {
            var startTime = DateTime.Now;
            Logger.LogInformation("开始执行设置: {Count}个参数", setDataList?.Count ?? 0);

            if (!IsInitialized)
            {
                Logger.LogWarning("执行设置失败: 设备未初始化");
                return Result<bool>.Failed("设备未初始化");
            }

            if (setDataList == null || setDataList.Count == 0)
            {
                Logger.LogWarning("执行设置失败: 设置参数不能为空");
                return Result<bool>.Failed("设置参数不能为空");
            }

            foreach (var setData in setDataList)
            {
                Logger.LogDebug("设置参数: {SetName} = {Value}", setData.SetName, setData.SetValue);

                if (!_setMethods.TryGetValue(setData.SetName, out var method))
                {
                    Logger.LogWarning("执行设置失败: 未找到设置参数 {SetName}", setData.SetName);
                    return Result<bool>.Failed($"未找到设置参数: {setData.SetName}");
                }

                try
                {
                    var argument = ConvertSetValue(setData.SetValue, method.GetParameters()[0].ParameterType);
                    var invocationResult = method.Invoke(this, new[] { argument });
                    var value = await AwaitInvocationResultAsync(invocationResult);
                    if (value is Result<bool> sdkResult && (!sdkResult.IsSuccess || sdkResult.Data == false))
                    {
                        Logger.LogWarning("设置参数失败: {SetName}, {Message}", setData.SetName, sdkResult.Message);
                        return sdkResult.IsSuccess
                            ? Result<bool>.Failed($"设置参数 {setData.SetName} 失败")
                            : sdkResult;
                    }

                    if (value is bool success && !success)
                    {
                        Logger.LogWarning("设置参数失败: {SetName}", setData.SetName);
                        return Result<bool>.Failed($"设置参数 {setData.SetName} 失败");
                    }

                    Logger.LogDebug("设置参数成功: {SetName} = {Value}", setData.SetName, setData.SetValue);
                }
                catch (Exception ex)
                {
                    ex = UnwrapInvocationException(ex);
                    Logger.LogError(ex, "设置参数异常: {SetName}", setData.SetName);
                    return Result<bool>.Failed($"设置参数 {setData.SetName} 失败: {ex.Message}");
                }
            }

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Logger.LogInformation("设置执行完成: {Count}个参数, 耗时: {Elapsed}ms", setDataList.Count, elapsed);
            return Result<bool>.Success(true);
        }

        /// <summary>
        /// 执行设置（同步版本）
        /// </summary>
        public virtual Result<bool> ExecuteSet(List<SetData> setDataList)
        {
            return ExecuteSetAsync(setDataList).GetAwaiter().GetResult();
        }

        #endregion

        #region 获取接口

        /// <summary>
        /// 获取状态
        /// </summary>
        public virtual Result<List<GetReturn>> GetStatus()
        {
            if (!IsInitialized)
                return Result<List<GetReturn>>.Failed("设备未初始化");

            var result = new List<GetReturn>();

            foreach (var kvp in _getMethods)
            {
                try
                {
                    var method = kvp.Value;
                    var invocationResult = method.Invoke(this, Array.Empty<object>());
                    var value = AwaitInvocationResultAsync(invocationResult).GetAwaiter().GetResult();
                    var stringValue = value?.ToString();

                    result.Add(new GetReturn
                    {
                        GetName = kvp.Key,
                        GetValue = stringValue ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    ex = UnwrapInvocationException(ex);
                    result.Add(new GetReturn
                    {
                        GetName = kvp.Key,
                        GetValue = $"Error: {ex.Message}"
                    });
                }
            }

            return Result<List<GetReturn>>.Success(result);
        }

        #endregion

        #region 进出接口

        /// <summary>
        /// 执行进出操作
        /// </summary>
        public virtual async Task<Result<Finish>> ExecuteEnterExitAsync(EnterOrExitData data)
        {
            if (!IsInitialized)
                return Result<Finish>.Failed("设备未初始化");

            if (string.IsNullOrEmpty(data?.EnterOrExitName))
                return Result<Finish>.Failed("进出操作名称不能为空");

            if (!_enterExitMethods.TryGetValue(data.EnterOrExitName, out var method))
                return Result<Finish>.Failed($"未找到进出操作: {data.EnterOrExitName}");

            try
            {
                var invocationResult = method.Invoke(this, new object[] { data });
                var value = await AwaitInvocationResultAsync(invocationResult);
                return value switch
                {
                    Result<Finish> sdkResult => sdkResult,
                    Finish finish => Result<Finish>.Success(finish),
                    _ => Result<Finish>.Success(Finish.Success())
                };
            }
            catch (Exception ex)
            {
                ex = UnwrapInvocationException(ex);
                return Result<Finish>.Failed($"执行进出操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行进出操作（同步版本）
        /// </summary>
        public virtual Result<Finish> ExecuteEnterExit(EnterOrExitData data)
        {
            return ExecuteEnterExitAsync(data).GetAwaiter().GetResult();
        }

        #endregion

        #region 心跳接口

        /// <summary>
        /// 获取心跳状态
        /// </summary>
        public virtual Result<HeartBeatInfo> GetHeartBeat()
        {
            try
            {
                var heartBeat = !IsInitialized
                    ? HeartBeatInfo.Monitoring()
                    : _communication != null && !IsConnected
                        ? HeartBeatInfo.EquipmentAbnormal()
                        : _resourceSemaphore.CurrentCount == 0
                            ? HeartBeatInfo.Monitoring()
                            : HeartBeatInfo.Normal();

                return Result<HeartBeatInfo>.Success(heartBeat);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "获取心跳失败");
                return Result<HeartBeatInfo>.Failed($"获取心跳失败: {ex.Message}");
            }
        }

        #endregion

        #region 信息注册接口

        /// <summary>
        /// 获取注册信息
        /// </summary>
        public virtual Result<RegisterInfo> GetRegisterInfo()
        {
            // 处理设备图标
            var equipmentIcon = GetEquipmentIcon();

            var basicInfo = new BasicInfo
            {
                EquipmentName = DriverAttribute.Name,
                EquipmentNameEN = DriverAttribute.NameEN ?? DriverAttribute.Name,
                EquipmentModel = DriverAttribute.Model ?? string.Empty,
                EquipmentManufacturer = DriverAttribute.Manufacturer ?? string.Empty,
                Version = DriverAttribute.Version,
                Author = DriverAttribute.Author,
                EquipmentClass = DriverAttribute.EquipmentClass,
                EquipmentType = DriverAttribute.EquipmentType,
                FunctionalResources = DriverAttribute.FunctionalResources,
                CanEmergencyStop = DriverAttribute.CanEmergencyStop ? 1 : 0,
                RuntimeAccessibility = DriverAttribute.RuntimeAccessibility,
                Parallelizability = DriverAttribute.Parallelizability,
                EquipmentIcon = equipmentIcon
            };

            var advancedInfo = new AdvancedInfo
            {
                EquipmentFunctions = GetEquipmentFunctions(),
                EquipmentGetInfos = GetEquipmentGetInfos(),
                EquipmentSetInfos = GetEquipmentSetInfos(),
                EquipmentNests = GetEquipmentNests(),
                EquipmentOperations = GetEquipmentOperations(),
                EquipmentEnterAndExit = GetEquipmentEnterAndExit()
            };

            return Result<RegisterInfo>.Success(new RegisterInfo
            {
                BasicInfo = basicInfo,
                AdvancedInfo = advancedInfo
            });
        }

        /// <summary>
        /// 获取设备图标（处理Icon和IconFile属性）
        /// </summary>
        private string GetEquipmentIcon()
        {
            // 优先使用直接设置的Base64图标
            if (!string.IsNullOrEmpty(DriverAttribute.Icon))
            {
                return DriverAttribute.Icon;
            }

            // 尝试从文件加载
            if (!string.IsNullOrEmpty(DriverAttribute.IconFile))
            {
                var icon = IconHelper.LoadIcon(DriverAttribute.IconFile);
                if (!string.IsNullOrEmpty(icon))
                {
                    return icon;
                }
            }

            // 使用默认图标
            return IconHelper.DefaultEquipmentIcon;
        }

        private List<EquipmentFunction> GetEquipmentFunctions()
        {
            var result = new List<EquipmentFunction>();

            foreach (var kvp in _functionMethods)
            {
                var attr = kvp.Value.GetCustomAttribute<DeviceFunctionAttribute>();
                if (attr != null)
                {
                    // 处理功能图标
                    var (iconBlack, iconWhite) = GetFunctionIcons(attr);

                    result.Add(new EquipmentFunction
                    {
                        FunctionName = attr.Name,
                        FunctionTitleCN = attr.TitleCN ?? attr.Name,
                        FunctionTitleEN = attr.TitleEN ?? attr.Name,
                        FunctionDescription = attr.Description ?? string.Empty,
                        FunctionDefaultPeriod = attr.DefaultPeriod.ToString(),
                        FunctionCategoryCN = attr.CategoryCN ?? string.Empty,
                        FunctionCategoryEN = attr.CategoryEN ?? string.Empty,
                        FunctionFormJsonStructure = attr.FormJson,
                        IconBlack = iconBlack,
                        IconWhite = iconWhite
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 获取功能图标（处理IconBlack/IconWhite和IconFileBlack/IconFileWhite属性）
        /// </summary>
        private (string iconBlack, string iconWhite) GetFunctionIcons(DeviceFunctionAttribute attr)
        {
            string iconBlack = null;
            string iconWhite = null;

            // 处理黑色图标
            if (!string.IsNullOrEmpty(attr.IconBlack))
            {
                iconBlack = attr.IconBlack;
            }
            else if (!string.IsNullOrEmpty(attr.IconFileBlack))
            {
                iconBlack = IconHelper.LoadIcon(attr.IconFileBlack);
            }

            // 如果没有设置黑色图标，使用默认
            if (string.IsNullOrEmpty(iconBlack))
            {
                iconBlack = IconHelper.DefaultFunctionIconBlack;
            }

            // 处理白色图标
            if (!string.IsNullOrEmpty(attr.IconWhite))
            {
                iconWhite = attr.IconWhite;
            }
            else if (!string.IsNullOrEmpty(attr.IconFileWhite))
            {
                iconWhite = IconHelper.LoadIcon(attr.IconFileWhite);
            }

            // 如果没有设置白色图标，使用默认
            if (string.IsNullOrEmpty(iconWhite))
            {
                iconWhite = IconHelper.DefaultFunctionIconWhite;
            }

            return (iconBlack, iconWhite);
        }

        private List<EquipmentGetInfo> GetEquipmentGetInfos()
        {
            var result = new List<EquipmentGetInfo>();

            foreach (var kvp in _getMethods)
            {
                var attr = kvp.Value.GetCustomAttribute<DeviceGetAttribute>();
                if (attr != null)
                {
                    result.Add(new EquipmentGetInfo
                    {
                        GetName = attr.Name,
                        GetTitleCN = attr.TitleCN ?? attr.Name,
                        GetTitleEN = attr.TitleEN ?? attr.Name,
                        GetType = attr.Type,
                        GetUnit = attr.Unit,
                        Description = attr.Description ?? string.Empty
                    });
                }
            }

            return result;
        }

        private List<EquipmentSetInfo> GetEquipmentSetInfos()
        {
            var result = new List<EquipmentSetInfo>();

            foreach (var kvp in _setMethods)
            {
                var attr = kvp.Value.GetCustomAttribute<DeviceSetAttribute>();
                if (attr != null)
                {
                    result.Add(new EquipmentSetInfo
                    {
                        SetName = attr.Name,
                        SetTitleCN = attr.TitleCN ?? attr.Name,
                        SetTitleEN = attr.TitleEN ?? attr.Name,
                        SetType = attr.Type,
                        SetUnit = attr.Unit,
                        Description = attr.Description ?? string.Empty
                    });
                }
            }

            return result;
        }

        private List<EquipmentOperation> GetEquipmentOperations()
        {
            var result = new List<EquipmentOperation>();

            foreach (var kvp in _operationMethods)
            {
                var attr = kvp.Value.GetCustomAttribute<DeviceOperationAttribute>();
                if (attr != null)
                {
                    result.Add(new EquipmentOperation
                    {
                        OperationName = attr.Name,
                        OperationTitleCN = attr.TitleCN ?? attr.Name,
                        OperationTitleEN = attr.TitleEN ?? attr.Name,
                        OperationDescription = attr.Description ?? string.Empty,
                        OperationFormJsonStructure = attr.FormJson ?? string.Empty
                    });
                }
            }

            return result;
        }

        private EquipmentEnterAndExit GetEquipmentEnterAndExit()
        {
            foreach (var method in _enterExitMethods.Values)
            {
                var attr = method.GetCustomAttribute<DeviceEnterExitAttribute>();
                if (attr != null)
                {
                    return new EquipmentEnterAndExit
                    {
                        EnterAndExitName = attr.Name,
                        EnterAndExitTitleCN = attr.TitleCN ?? attr.Name,
                        EnterAndExitTitleEN = attr.TitleEN ?? attr.Name
                    };
                }
            }

            return null;
        }

        private List<EquipmentNest> GetEquipmentNests()
        {
            var result = new List<EquipmentNest>();

            foreach (var property in _nestProperties)
            {
                try
                {
                    var nest = property.GetValue(this) as EquipmentNest;
                    if (nest != null)
                    {
                        result.Add(nest);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "获取位置属性 {PropertyName} 失败", property.Name);
                }
            }

            var dynamicNests = GetDynamicEquipmentNests();
            if (dynamicNests != null)
            {
                foreach (var nest in dynamicNests)
                    if (nest != null) result.Add(nest);
            }

            return result;
        }

        /// <summary>由配置或设备发现生成的运行时位置。</summary>
        protected virtual IEnumerable<EquipmentNest> GetDynamicEquipmentNests() =>
            Enumerable.Empty<EquipmentNest>();

        #endregion

        #region 通信辅助方法

        /// <summary>
        /// 发送数据
        /// </summary>
        protected async Task<bool> SendAsync(byte[] data)
        {
            if (_communication == null || !IsConnected)
                return false;

            return await _communication.SendAsync(data);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        protected async Task<byte[]> ReceiveAsync()
        {
            if (_communication == null || !IsConnected)
                return null;

            return await _communication.ReceiveAsync();
        }

        /// <summary>
        /// 发送并接收
        /// </summary>
        protected async Task<byte[]> SendAndReceiveAsync(byte[] data)
        {
            if (_communication == null || !IsConnected)
                return null;

            return await _communication.SendAndReceiveAsync(data);
        }

        /// <summary>原子发送并读取指定长度响应；仅适用于TCP/Serial。</summary>
        protected Task<byte[]> SendAndReadExactAsync(byte[] data, int responseLength,
            CancellationToken cancellationToken = default)
        {
            return GetFramedCommunication().SendAndReadExactAsync(
                data, responseLength, cancellationToken);
        }

        /// <summary>原子发送并读取到结束字节；仅适用于TCP/Serial。</summary>
        protected Task<byte[]> SendAndReadUntilAsync(byte[] data, byte endByte,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default)
        {
            return GetFramedCommunication().SendAndReadUntilAsync(
                data, endByte, maxLength, cancellationToken);
        }

        private IFramedCommunication GetFramedCommunication()
        {
            if (_communication is not IFramedCommunication framed)
                throw new InvalidOperationException("当前通信类型不支持定长帧或结束字节帧");
            return framed;
        }

        /// <summary>
        /// 同步发送数据。
        /// </summary>
        protected bool Send(byte[] data)
        {
            return _communication != null && IsConnected && _communication.Send(data);
        }

        /// <summary>
        /// 同步接收数据。
        /// </summary>
        protected byte[] Receive()
        {
            return _communication != null && IsConnected ? _communication.Receive() : null;
        }

        /// <summary>
        /// 同步发送并接收数据。
        /// </summary>
        protected byte[] SendAndReceive(byte[] data)
        {
            return _communication != null && IsConnected ? _communication.SendAndReceive(data) : null;
        }

        #endregion

        #region JSON辅助方法

        /// <summary>
        /// 将OperationParam/FunctionParam等object类型的JSON属性安全转换为JSON字符串
        /// System.Text.Json反序列化object类型时会生成JsonElement，需要用GetRawText()获取原始JSON
        /// </summary>
        protected static string ToJsonString(object obj)
        {
            if (obj == null) return "";
            if (obj is JsonElement element) return element.GetRawText();
            return obj.ToString() ?? "";
        }

        /// <summary>
        /// 将OperationParam/FunctionParam安全反序列化为指定类型
        /// </summary>
        protected static T DeserializeParam<T>(object obj)
        {
            if (obj == null)
                return default;
            if (obj is T typed)
                return typed;
            if (obj is JsonElement element)
                return element.Deserialize<T>(ParameterJsonOptions);

            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj), ParameterJsonOptions);
        }

        /// <summary>读取并校验必填的Function参数模型。</summary>
        protected static T RequireFunctionParam<T>(FunctionData data) =>
            DeserializeRequiredParam<T>(data?.FunctionParam, "functionParam");

        /// <summary>读取并校验必填的Operation参数模型。</summary>
        protected static T RequireOperationParam<T>(OperationData data) =>
            DeserializeRequiredParam<T>(data?.OperationParam, "operationParam");

        /// <summary>读取并校验必填的进出板参数模型。</summary>
        protected static T RequireEnterExitValue<T>(EnterOrExitData data) =>
            DeserializeRequiredParam<T>(data?.EnterOrExitValue, "enterOrExitValue");

        private static T DeserializeRequiredParam<T>(object value, string fieldName)
        {
            if (value == null)
                throw new ArgumentException($"{fieldName}不能为空");

            T model;
            try
            {
                model = DeserializeParam<T>(value);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{fieldName}格式错误: {ex.Message}", fieldName, ex);
            }

            if (model == null)
                throw new ArgumentException($"{fieldName}不能为空");

            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(model, new ValidationContext(model), validationResults, true))
            {
                throw new ArgumentException($"{fieldName}校验失败: " +
                    string.Join("; ", validationResults.Select(result => result.ErrorMessage)));
            }

            return model;
        }

        private static object ConvertSetValue(string value, Type targetType)
        {
            if (targetType == typeof(string) || targetType == typeof(object))
                return value;

            var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (string.IsNullOrWhiteSpace(value) && Nullable.GetUnderlyingType(targetType) != null)
                return null;
            if (actualType.IsEnum)
                return Enum.Parse(actualType, value, ignoreCase: true);
            if (actualType == typeof(bool) && bool.TryParse(value, out var boolean))
                return boolean;

            try
            {
                var json = value?.TrimStart().StartsWith("{") == true ||
                           value?.TrimStart().StartsWith("[") == true
                    ? value
                    : JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize(json, targetType, ParameterJsonOptions);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"设置值无法转换为{targetType.Name}: {ex.Message}", ex);
            }
        }

        private static readonly JsonSerializerOptions ParameterJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        private static async Task<object> AwaitInvocationResultAsync(object invocationResult)
        {
            if (invocationResult == null)
                return null;

            if (invocationResult is Task task)
            {
                await task.ConfigureAwait(false);
                return task.GetType().IsGenericType
                    ? task.GetType().GetProperty("Result")?.GetValue(task)
                    : null;
            }

            var resultType = invocationResult.GetType();
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var asTask = resultType.GetMethod("AsTask")?.Invoke(invocationResult, null) as Task;
                if (asTask != null)
                {
                    await asTask.ConfigureAwait(false);
                    return asTask.GetType().GetProperty("Result")?.GetValue(asTask);
                }
            }

            if (invocationResult is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
                return null;
            }

            return invocationResult;
        }

        private static Exception UnwrapInvocationException(Exception exception)
        {
            while (exception is TargetInvocationException { InnerException: not null } ||
                   exception is AggregateException { InnerException: not null })
            {
                exception = exception.InnerException;
            }

            return exception;
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                await DisconnectAsync();
            }
            finally
            {
                if (_connectionManager != null)
                {
                    await _connectionManager.DisposeAsync();
                    _connectionManager = null;
                    _communication = null;
                }
                var communication = _communication;
                _communication = null;
                if (communication is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (communication is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _resourceSemaphore.Dispose();
                _deviceCallSemaphore.Dispose();
            }
        }
    }
}

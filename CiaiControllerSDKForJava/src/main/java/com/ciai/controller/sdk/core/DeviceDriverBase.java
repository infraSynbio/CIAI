package com.ciai.controller.sdk.core;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.ciai.controller.sdk.annotation.*;
import com.ciai.controller.sdk.icons.IconHelper;
import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.interface_.IFramedCommunication;
import com.ciai.controller.sdk.interface_.DriverEventListener;
import com.ciai.controller.sdk.model.*;
import com.ciai.controller.sdk.service.CommunicationFactory;
import com.ciai.controller.sdk.service.ConnectionManager;
import com.ciai.controller.sdk.config.ConfigurationValidator;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.Function;

/**
 * 设备驱动基类
 */
public abstract class DeviceDriverBase implements AutoCloseable {

    protected final Logger logger = LoggerFactory.getLogger(getClass());
    protected static final ObjectMapper objectMapper = new ObjectMapper()
            .setDefaultPropertyInclusion(JsonInclude.Include.ALWAYS)
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    private DeviceConfiguration configuration;
    private ICommunication communication;
    private ConnectionManager connectionManager;
    private volatile boolean isInitialized = false;
    private final Semaphore resourceSemaphore;
    private Semaphore deviceCallSemaphore;
    private final DeviceDriver driverAttribute;
    private final Map<String, Method> functionMethods = new LinkedHashMap<>();
    private final Map<String, Method> operationMethods = new LinkedHashMap<>();
    private final Map<String, Method> setMethods = new LinkedHashMap<>();
    private final Map<String, Method> getMethods = new LinkedHashMap<>();
    private final Map<String, Method> enterExitMethods = new LinkedHashMap<>();
    private final ConcurrentMap<String, DriverExecutionContext> instructionContexts = new ConcurrentHashMap<>();
    private final ThreadLocal<DriverExecutionContext> currentExecution = new ThreadLocal<>();
    private final CopyOnWriteArrayList<DriverEventListener> eventListeners = new CopyOnWriteArrayList<>();

    public void addEventListener(DriverEventListener listener) { if (listener != null) eventListeners.addIfAbsent(listener); }
    public void removeEventListener(DriverEventListener listener) { eventListeners.remove(listener); }
    public boolean cancelInstruction(String instructionId) {
        DriverExecutionContext context = instructionContexts.get(instructionId);
        if (context == null) return false; context.cancel(); return true;
    }
    protected DriverExecutionContext getCurrentExecution() { return currentExecution.get(); }
    protected void reportProgress(double progress, String message, Object data) {
        if (progress < 0 || progress > 100) throw new IllegalArgumentException("Progress must be 0..100");
        publishEvent("progress", message, data, progress);
    }
    protected void publishEvent(String type, String message, Object data, Double progress) {
        if (isBlank(type)) throw new IllegalArgumentException("Event type is required");
        DriverExecutionContext c=currentExecution.get(); DriverEvent event=new DriverEvent();event.setType(type);
        if(c!=null){event.setInstructionId(c.getInstructionId());event.setNestId(c.getNestId());}
        event.setMessage(message);event.setData(data);event.setProgress(progress);
        for(DriverEventListener listener:eventListeners)try{listener.onEvent(event);}catch(RuntimeException e){logger.warn("Driver event listener failed",e);}
    }

    /**
     * 声明式驱动使用的无参构造函数。DriverHost会在初始化前注入YAML配置。
     */
    public DeviceDriverBase() {
        this(new DeviceConfiguration());
    }

    public DeviceDriverBase(DeviceConfiguration configuration) {
        this.driverAttribute = getClass().getAnnotation(DeviceDriver.class);
        if (driverAttribute == null) {
            throw new IllegalStateException(getClass().getName() + " must have @DeviceDriver");
        }
        int resources = driverAttribute.functionalResources();
        if (resources <= 0) {
            throw new IllegalArgumentException("functionalResources must be greater than zero");
        }
        this.resourceSemaphore = new Semaphore(resources, true);
        validateAndIndexContract();
        applyConfiguration(configuration);
    }

    /**
     * 由DriverHost在初始化前注入配置。普通驱动无需调用。
     */
    public synchronized void applyConfiguration(DeviceConfiguration configuration) {
        if (configuration == null) {
            throw new IllegalArgumentException("Device configuration is required");
        }
        if (isInitialized) {
            throw new IllegalStateException("Configuration cannot be changed after initialization");
        }
        if (configuration.getDeviceCallResources() <= 0 || configuration.getDeviceCallTimeout() <= 0) {
            throw new IllegalArgumentException("Device call resources and timeout must be greater than zero");
        }
        this.configuration = configuration;
        this.deviceCallSemaphore = new Semaphore(configuration.getDeviceCallResources(), true);
    }

    /**
     * 设置通信实现
     */
    protected void setCommunication(ICommunication communication) {
        this.communication = communication;
    }

    /** 按名称获取声明式连接；null表示默认连接。 */
    protected ICommunication getCommunication(String name) {
        if (connectionManager != null) return connectionManager.get(name);
        if (name == null || "default".equalsIgnoreCase(name)) return communication;
        throw new IllegalArgumentException("Unknown connection: " + name);
    }

    protected ICommunication getCommunication() { return getCommunication(null); }

    /** TCP/串口固定串行；HTTP/DLL/API遵循连接的maxConcurrency。 */
    protected <T> T executeConnectionCall(String name, Function<ICommunication,T> action) {
        if (connectionManager != null) return connectionManager.execute(name, action);
        return executeDeviceCall(() -> action.apply(getCommunication(name)));
    }

    /**
     * 获取配置
     */
    public DeviceConfiguration getConfiguration() {
        return configuration;
    }

    public int getDeviceCallResources() {
        return configuration.getDeviceCallResources();
    }

    /**
     * 是否已初始化
     */
    public boolean isInitialized() {
        return isInitialized;
    }

    /**
     * 是否已连接
     */
    public boolean isConnected() {
        return connectionManager != null ? connectionManager.isConnected()
                : communication != null && communication.isConnected();
    }

    /**
     * 获取驱动注解
     */
    public DeviceDriver getDriverAttribute() {
        return driverAttribute;
    }

    /**
     * 初始化
     */
    public CompletableFuture<Boolean> initializeAsync() {
        return CompletableFuture.supplyAsync(() -> {
            try {
                ConfigurationValidator.validateAndThrow(configuration);
                if (isInitialized) {
                    return true;
                }

                if (configuration.getConnections() != null && !configuration.getConnections().isEmpty()) {
                    connectionManager = new ConnectionManager(configuration.getConnections().values());
                    communication = connectionManager.get();
                    if (!connectionManager.connect()) {
                        connectionManager.close(); connectionManager = null; communication = null;
                        return false;
                    }
                    isInitialized = true;
                    logger.info("Device driver initialized with named connections");
                    return true;
                }

                if (communication == null && configuration.getCommunicationType() != CommunicationType.DLL) {
                    if (!CommunicationFactory.canCreate(configuration)) {
                        logger.error("Incomplete communication configuration: {}",
                                configuration.getCommunicationType());
                        return false;
                    }
                    communication = CommunicationFactory.create(configuration);
                }

                if (communication != null) {
                    boolean connected = communication.connect();
                    if (!connected) {
                        logger.error("Failed to connect to device");
                        return false;
                    }
                }

                isInitialized = true;
                logger.info("Device driver initialized successfully");
                return true;
            } catch (Exception e) {
                logger.error("Failed to initialize device driver", e);
                return false;
            }
        });
    }

    /**
     * 同步初始化
     */
    public boolean initialize() {
        try {
            return initializeAsync().get();
        } catch (Exception e) {
            logger.error("Initialize error", e);
            return false;
        }
    }

    /**
     * 断开连接
     */
    public CompletableFuture<Void> disconnectAsync() {
        return CompletableFuture.runAsync(() -> {
            if (communication != null) {
                if (connectionManager != null) connectionManager.disconnect();
                else communication.disconnect();
            }
            isInitialized = false;
            logger.info("Device driver disconnected");
        });
    }

    /**
     * 同步断开连接
     */
    public void disconnect() {
        try {
            disconnectAsync().get();
        } catch (Exception e) {
            logger.error("Disconnect error", e);
        }
    }

    // ==================== 功能接口 ====================

    /**
     * 执行功能（异步）
     */
    @SuppressWarnings("unchecked")
    public CompletableFuture<Result<Finish>> executeFunctionAsync(FunctionData data) {
        return CompletableFuture.supplyAsync(() -> {
            if (!isInitialized) {
                return Result.failed("Device is not initialized");
            }
            if (data == null || isBlank(data.getFunctionName())) {
                return Result.failed("Function name is required");
            }
            try {
                Method method = functionMethods.get(data.getFunctionName());
                if (method == null) {
                    logger.error("Function not found: {}", data.getFunctionName());
                    return Result.failed("Function not found: " + data.getFunctionName());
                }

                // 获取资源
                if (!acquireResource()) {
                    return Result.failed("Failed to acquire resource");
                }

                try {
                    DriverExecutionContext execution = new DriverExecutionContext(data.getInstructionId(), data.getNestId());
                    currentExecution.set(execution);
                    if (!isBlank(data.getInstructionId())) instructionContexts.put(data.getInstructionId(), execution);
                    Object result = awaitInvocationResult(method.invoke(this, data));
                    if (result instanceof Result) {
                        Result<Finish> sdkResult = (Result<Finish>) result;
                        completeFinishContext(sdkResult.getData(), data.getInstructionId(), data.getNestId());
                        return sdkResult;
                    }
                    if (result instanceof Finish) {
                        Finish finish = (Finish) result;
                        completeFinishContext(finish, data.getInstructionId(), data.getNestId());
                        return Result.success(finish);
                    }
                    Finish finish = Finish.success();
                    completeFinishContext(finish, data.getInstructionId(), data.getNestId());
                    return Result.success(finish);
                } finally {
                    currentExecution.remove();
                    if (!isBlank(data.getInstructionId())) instructionContexts.remove(data.getInstructionId());
                    releaseResource();
                }
            } catch (CancellationException e) {
                logger.info("Function cancelled: {}, instructionId: {}",
                        data.getFunctionName(), data.getInstructionId());
                return Result.failed("Instruction cancelled");
            } catch (Exception e) {
                Throwable cause = unwrapInvocationException(e);
                if (cause instanceof CancellationException) {
                    logger.info("Function cancelled: {}, instructionId: {}",
                            data.getFunctionName(), data.getInstructionId());
                    return Result.failed("Instruction cancelled");
                }
                logger.error("Execute function error: {}", data.getFunctionName(), cause);
                return Result.failed("Execute function error: " + cause.getMessage());
            }
        });
    }

    /**
     * 执行功能（同步）
     */
    public Result<Finish> executeFunction(FunctionData data) {
        try {
            return executeFunctionAsync(data).get();
        } catch (Exception e) {
            logger.error("Execute function sync error", e);
            return Result.failed("Execute function error: " + e.getMessage());
        }
    }

    // ==================== 操作接口 ====================

    /**
     * 执行操作（异步）
     */
    @SuppressWarnings("unchecked")
    public CompletableFuture<Result<Boolean>> executeOperationAsync(OperationData data) {
        return CompletableFuture.supplyAsync(() -> {
            if (!isInitialized) {
                return Result.failed("Device is not initialized");
            }
            if (data == null || isBlank(data.getOperationName())) {
                return Result.failed("Operation name is required");
            }
            try {
                Method method = operationMethods.get(data.getOperationName());
                if (method == null) {
                    logger.error("Operation not found: {}", data.getOperationName());
                    return Result.failed("Operation not found: " + data.getOperationName());
                }

                Object result = awaitInvocationResult(method.invoke(this, data));
                if (result instanceof Result) {
                    return (Result<Boolean>) result;
                }
                if (result instanceof Boolean) {
                    return Result.success((Boolean) result);
                }
                return Result.success(true);
            } catch (Exception e) {
                Throwable cause = unwrapInvocationException(e);
                logger.error("Execute operation error: {}", data.getOperationName(), cause);
                return Result.failed("Execute operation error: " + cause.getMessage());
            }
        });
    }

    /**
     * 执行操作（同步）
     */
    public Result<Boolean> executeOperation(OperationData data) {
        try {
            return executeOperationAsync(data).get();
        } catch (Exception e) {
            logger.error("Execute operation sync error", e);
            return Result.failed("Execute operation error: " + e.getMessage());
        }
    }

    // ==================== 设置接口 ====================

    /**
     * 执行设置（异步）
     */
    public CompletableFuture<Result<Boolean>> executeSetAsync(List<SetData> setDataList) {
        return CompletableFuture.supplyAsync(() -> {
            if (!isInitialized) {
                return Result.failed("Device is not initialized");
            }
            if (setDataList == null || setDataList.isEmpty()) {
                return Result.failed("Set data is required");
            }
            try {
                for (SetData setData : setDataList) {
                    if (setData == null || isBlank(setData.getSetName())) {
                        return Result.failed("Set name is required");
                    }
                    Method method = setMethods.get(setData.getSetName());
                    if (method == null) {
                        logger.error("Set not found: {}", setData.getSetName());
                        return Result.failed("Set not found: " + setData.getSetName());
                    }

                    Object argument = convertValue(setData.getSetValue(), method.getParameterTypes()[0]);
                    Object result = awaitInvocationResult(method.invoke(this, argument));
                    if (result instanceof Result) {
                        Result<?> r = (Result<?>) result;
                        if (!r.isSuccess()) {
                            return Result.failed(r.getCode(), r.getMessage());
                        }
                        if (Boolean.FALSE.equals(r.getData())) {
                            return Result.failed("Set failed: " + setData.getSetName());
                        }
                    } else if (Boolean.FALSE.equals(result)) {
                        return Result.failed("Set failed: " + setData.getSetName());
                    }
                }
                return Result.success(true);
            } catch (Exception e) {
                Throwable cause = unwrapInvocationException(e);
                logger.error("Execute set error", cause);
                return Result.failed("Execute set error: " + cause.getMessage());
            }
        });
    }

    /**
     * 执行设置（同步）
     */
    public Result<Boolean> executeSet(List<SetData> setDataList) {
        try {
            return executeSetAsync(setDataList).get();
        } catch (Exception e) {
            logger.error("Execute set sync error", e);
            return Result.failed("Execute set error: " + e.getMessage());
        }
    }

    // ==================== 获取接口 ====================

    /**
     * 获取状态
     */
    public Result<List<GetReturn>> getStatus() {
        if (!isInitialized) {
            return Result.failed("Device is not initialized");
        }
        try {
            List<GetReturn> results = new ArrayList<>();
            for (Map.Entry<String, Method> entry : getMethods.entrySet()) {
                try {
                    Object value = awaitInvocationResult(entry.getValue().invoke(this));
                    results.add(new GetReturn(entry.getKey(), value != null ? value.toString() : ""));
                } catch (Exception e) {
                    Throwable cause = unwrapInvocationException(e);
                    logger.error("Get status error for: {}", entry.getKey(), cause);
                    results.add(new GetReturn(entry.getKey(), "Error: " + cause.getMessage()));
                }
            }

            return Result.success(results);
        } catch (Exception e) {
            logger.error("Get status error", e);
            return Result.failed("Get status error: " + e.getMessage());
        }
    }

    // ==================== 进出接口 ====================

    /**
     * 执行进出操作（异步）
     */
    @SuppressWarnings("unchecked")
    public CompletableFuture<Result<Finish>> executeEnterExitAsync(EnterOrExitData data) {
        return CompletableFuture.supplyAsync(() -> {
            if (!isInitialized) {
                return Result.failed("Device is not initialized");
            }
            if (data == null || isBlank(data.getEnterOrExitName())) {
                return Result.failed("Enter/exit name is required");
            }
            try {
                Method method = enterExitMethods.get(data.getEnterOrExitName());
                if (method == null) {
                    logger.error("EnterExit not found: {}", data.getEnterOrExitName());
                    return Result.failed("EnterExit not found: " + data.getEnterOrExitName());
                }

                Object result = awaitInvocationResult(method.invoke(this, data));
                if (result instanceof Result) {
                    return (Result<Finish>) result;
                }
                if (result instanceof Finish) {
                    return Result.success((Finish) result);
                }
                return Result.success(Finish.success());
            } catch (Exception e) {
                Throwable cause = unwrapInvocationException(e);
                logger.error("Execute enter/exit error: {}", data.getEnterOrExitName(), cause);
                return Result.failed("Execute enter/exit error: " + cause.getMessage());
            }
        });
    }

    /**
     * 执行进出操作（同步）
     */
    public Result<Finish> executeEnterExit(EnterOrExitData data) {
        try {
            return executeEnterExitAsync(data).get();
        } catch (Exception e) {
            logger.error("Execute enter/exit sync error", e);
            return Result.failed("Execute enter/exit error: " + e.getMessage());
        }
    }

    // ==================== 心跳接口 ====================

    /**
     * 获取心跳信息
     */
    public Result<HeartBeatInfo> getHeartBeat() {
        try {
            HeartBeatInfo heartBeat;

            if (!isInitialized) {
                heartBeat = HeartBeatInfo.monitoring();
            } else if (communication != null && !isConnected()) {
                heartBeat = HeartBeatInfo.equipmentAbnormal();
            } else if (resourceSemaphore.availablePermits() == 0) {
                heartBeat = HeartBeatInfo.monitoring();
            } else {
                heartBeat = HeartBeatInfo.normal();
            }

            return Result.success(heartBeat);
        } catch (Exception e) {
            logger.error("Get heartbeat error", e);
            return Result.failed("Get heartbeat error: " + e.getMessage());
        }
    }

    // ==================== 注册信息接口 ====================

    /**
     * 获取注册信息
     */
    public Result<RegisterInfo> getRegisterInfo() {
        try {
            RegisterInfo registerInfo = new RegisterInfo();

            // 设置心跳信息
            Result<HeartBeatInfo> heartBeatResult = getHeartBeat();
            // 心跳信息不放在RegisterInfo中

            // 设置基础信息
            RegisterInfo.BasicInfo basicInfo = new RegisterInfo.BasicInfo();
            if (driverAttribute != null) {
                basicInfo.setEquipmentName(driverAttribute.name());
                basicInfo.setEquipmentNameEN(isBlank(driverAttribute.nameEN())
                        ? driverAttribute.name() : driverAttribute.nameEN());
                basicInfo.setEquipmentModel(driverAttribute.model());
                basicInfo.setEquipmentManufacturer(driverAttribute.manufacturer());
                basicInfo.setVersion(driverAttribute.version());
                basicInfo.setAuthor(driverAttribute.author());
                basicInfo.setEquipmentClass(driverAttribute.equipmentClass());
                basicInfo.setEquipmentType(driverAttribute.equipmentType());
                basicInfo.setFunctionalResources(driverAttribute.functionalResources());
                basicInfo.setCanEmergencyStop(driverAttribute.canEmergencyStop() ? 1 : 0);
                basicInfo.setRuntimeAccessibility(driverAttribute.runtimeAccessibility());
                basicInfo.setParallelizability(driverAttribute.parallelizability());

                // 加载图标
                String icon = driverAttribute.icon();
                if (icon == null || icon.isEmpty()) {
                    String iconFile = driverAttribute.iconFile();
                    if (iconFile != null && !iconFile.isEmpty()) {
                        icon = IconHelper.loadIcon(iconFile);
                    }
                    // 如果没有指定iconFile或加载失败，使用默认图标
                    if (icon == null || icon.isEmpty()) {
                        icon = IconHelper.getDefaultEquipmentIcon();
                    }
                }
                basicInfo.setEquipmentIcon(icon);
            }
            registerInfo.setBasicInfo(basicInfo);

            // 设置高级信息
            RegisterInfo.AdvancedInfo advancedInfo = new RegisterInfo.AdvancedInfo();
            registerInfo.setAdvancedInfo(advancedInfo);

            // 扫描方法获取功能、操作、设置、获取、进出信息
            scanMethods(registerInfo);

            // 扫描字段获取位置信息
            scanFields(registerInfo);

            return Result.success(registerInfo);
        } catch (Exception e) {
            logger.error("Get register info error", e);
            return Result.failed("Get register info error: " + e.getMessage());
        }
    }

    // ==================== 通信辅助方法 ====================

    /**
     * 异步发送数据
     */
    protected CompletableFuture<Boolean> sendAsync(byte[] data) {
        if (communication == null) {
            return CompletableFuture.completedFuture(false);
        }
        return communication.sendAsync(data);
    }

    /**
     * 异步接收数据
     */
    protected CompletableFuture<byte[]> receiveAsync() {
        if (communication == null) {
            return CompletableFuture.completedFuture(null);
        }
        return communication.receiveAsync();
    }

    /**
     * 异步发送并接收数据
     */
    protected CompletableFuture<byte[]> sendAndReceiveAsync(byte[] data) {
        if (communication == null) {
            return CompletableFuture.completedFuture(null);
        }
        return communication.sendAndReceiveAsync(data);
    }

    /**
     * 同步发送数据
     */
    protected boolean send(byte[] data) {
        if (communication == null) {
            return false;
        }
        return communication.send(data);
    }

    /**
     * 同步接收数据
     */
    protected byte[] receive() {
        if (communication == null) {
            return null;
        }
        return communication.receive();
    }

    /**
     * 同步发送并接收数据
     */
    protected byte[] sendAndReceive(byte[] data) {
        if (communication == null) {
            return null;
        }
        return communication.sendAndReceive(data);
    }

    protected CompletableFuture<byte[]> sendAndReadExactAsync(byte[] data, int responseLength) {
        return framedCommunication().sendAndReadExactAsync(data, responseLength);
    }

    protected CompletableFuture<byte[]> sendAndReadUntilAsync(
            byte[] data, byte endByte, int maxLength) {
        return framedCommunication().sendAndReadUntilAsync(data, endByte, maxLength);
    }

    protected byte[] sendAndReadExact(byte[] data, int responseLength) {
        return framedCommunication().sendAndReadExact(data, responseLength);
    }

    protected byte[] sendAndReadUntil(byte[] data, byte endByte, int maxLength) {
        return framedCommunication().sendAndReadUntil(data, endByte, maxLength);
    }

    private IFramedCommunication framedCommunication() {
        if (!(communication instanceof IFramedCommunication)) {
            throw new IllegalStateException(
                    "Configured communication does not support exact or delimited frames");
        }
        return (IFramedCommunication) communication;
    }

    // ==================== 资源管理 ====================

    /**
     * 异步获取资源
     */
    protected CompletableFuture<Boolean> acquireResourceAsync() {
        return CompletableFuture.supplyAsync(() -> acquireResource());
    }

    /**
     * 同步获取资源
     */
    protected boolean acquireResource() {
        try {
            return resourceSemaphore.tryAcquire(30, TimeUnit.SECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return false;
        }
    }

    /**
     * 尝试获取资源（非阻塞）
     */
    protected boolean tryAcquireResource() {
        return resourceSemaphore.tryAcquire();
    }

    /**
     * 释放资源
     */
    protected void releaseResource() {
        resourceSemaphore.release();
    }

    /**
     * 在独立的底层设备调用资源内执行DLL/厂商API操作。
     * 只包住真实设备调用，不要包住整个长时间业务Function。
     */
    protected <T> T executeDeviceCall(Callable<T> action) {
        boolean acquired = false;
        try {
            acquired = deviceCallSemaphore.tryAcquire(
                    configuration.getDeviceCallTimeout(), TimeUnit.MILLISECONDS);
            if (!acquired) {
                throw new IllegalStateException("Timed out waiting for a device-call resource");
            }
            return action.call();
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while waiting for a device-call resource", e);
        } catch (RuntimeException e) {
            throw e;
        } catch (Exception e) {
            throw new RuntimeException(e);
        } finally {
            if (acquired) {
                deviceCallSemaphore.release();
            }
        }
    }

    protected void executeDeviceCall(Runnable action) {
        executeDeviceCall(() -> {
            action.run();
            return Boolean.TRUE;
        });
    }

    protected <T> CompletableFuture<T> executeDeviceCallAsync(Callable<T> action) {
        return CompletableFuture.supplyAsync(() -> executeDeviceCall(action));
    }

    /**
     * 将FunctionParam、OperationParam或EnterOrExitValue转换成强类型参数。
     * 驱动只需要声明参数模型，无需了解Jackson节点或Map细节。
     */
    protected <T> T deserializeParam(Object value, Class<T> targetType) {
        if (targetType == null) {
            throw new IllegalArgumentException("Target parameter type is required");
        }
        if (value == null) {
            return null;
        }
        return objectMapper.convertValue(value, targetType);
    }

    protected <T> T requireFunctionParam(FunctionData data, Class<T> targetType) {
        return requireParam(data == null ? null : data.getFunctionParam(), targetType, "functionParam");
    }

    protected <T> T requireOperationParam(OperationData data, Class<T> targetType) {
        return requireParam(data == null ? null : data.getOperationParam(), targetType, "operationParam");
    }

    protected <T> T requireEnterExitValue(EnterOrExitData data, Class<T> targetType) {
        return requireParam(data == null ? null : data.getEnterOrExitValue(), targetType,
                "enterOrExitValue");
    }

    private <T> T requireParam(Object value, Class<T> targetType, String fieldName) {
        if (value == null) {
            throw new IllegalArgumentException(fieldName + " is required");
        }
        try {
            T result = deserializeParam(value, targetType);
            if (result == null) {
                throw new IllegalArgumentException(fieldName + " is required");
            }
            return result;
        } catch (IllegalArgumentException e) {
            throw new IllegalArgumentException(fieldName + " is invalid: " + e.getMessage(), e);
        }
    }

    // ==================== 私有辅助方法 ====================

    private void validateAndIndexContract() {
        for (Method method : getClass().getMethods()) {
            DeviceFunction function = method.getAnnotation(DeviceFunction.class);
            if (function != null) {
                validateParameters(method, "@DeviceFunction", FunctionData.class);
                validateFormJson(method, function.formJson());
                registerUnique(functionMethods, function.name(), method, "@DeviceFunction");
            }

            DeviceOperation operation = method.getAnnotation(DeviceOperation.class);
            if (operation != null) {
                validateParameters(method, "@DeviceOperation", OperationData.class);
                validateFormJson(method, operation.formJson());
                registerUnique(operationMethods, operation.name(), method, "@DeviceOperation");
            }

            DeviceSet set = method.getAnnotation(DeviceSet.class);
            if (set != null) {
                if (method.getParameterCount() != 1) {
                    throw contractError(method, "@DeviceSet must declare exactly one parameter");
                }
                registerUnique(setMethods, set.name(), method, "@DeviceSet");
            }

            DeviceGet get = method.getAnnotation(DeviceGet.class);
            if (get != null) {
                validateParameters(method, "@DeviceGet");
                registerUnique(getMethods, get.name(), method, "@DeviceGet");
            }

            DeviceEnterExit enterExit = method.getAnnotation(DeviceEnterExit.class);
            if (enterExit != null) {
                validateParameters(method, "@DeviceEnterExit", EnterOrExitData.class);
                registerUnique(enterExitMethods, enterExit.name(), method, "@DeviceEnterExit");
            }

            DeviceNest nest = method.getAnnotation(DeviceNest.class);
            if (nest != null) {
                validateParameters(method, "@DeviceNest");
                if (!EquipmentNest.class.isAssignableFrom(method.getReturnType())) {
                    throw contractError(method, "@DeviceNest must return EquipmentNest");
                }
            }
        }
        if (enterExitMethods.size() > 1) {
            throw new IllegalStateException("Registration contract exposes only one @DeviceEnterExit; "
                    + "use one annotated method and dispatch enter/exit from its parameters");
        }
    }

    private void validateParameters(Method method, String annotationName, Class<?>... expectedTypes) {
        Class<?>[] actual = method.getParameterTypes();
        if (actual.length != expectedTypes.length) {
            throw contractError(method, annotationName + " parameter count is invalid; expected "
                    + Arrays.toString(expectedTypes));
        }
        for (int i = 0; i < actual.length; i++) {
            if (!actual[i].isAssignableFrom(expectedTypes[i])) {
                throw contractError(method, annotationName + " parameter " + (i + 1)
                        + " must accept " + expectedTypes[i].getSimpleName());
            }
        }
    }

    private void validateFormJson(Method method, String formJson) {
        if (isBlank(formJson)) {
            return;
        }
        try {
            objectMapper.readTree(formJson);
        } catch (Exception e) {
            throw contractError(method, "formJson is not valid JSON: " + e.getMessage());
        }
    }

    private void registerUnique(Map<String, Method> methods, String name, Method method,
                                String annotationName) {
        if (isBlank(name)) {
            throw contractError(method, annotationName + " name is required");
        }
        Method existing = methods.putIfAbsent(name, method);
        if (existing != null) {
            throw contractError(method, annotationName + " name is duplicated: " + name
                    + " (also used by " + existing.getName() + ")");
        }
    }

    private IllegalStateException contractError(Method method, String message) {
        return new IllegalStateException(getClass().getSimpleName() + "." + method.getName()
                + ": " + message);
    }

    private Object awaitInvocationResult(Object value) throws Exception {
        Object current = value;
        while (current instanceof CompletionStage) {
            try {
                current = ((CompletionStage<?>) current).toCompletableFuture().get();
            } catch (ExecutionException e) {
                Throwable cause = e.getCause();
                if (cause instanceof Exception) {
                    throw (Exception) cause;
                }
                throw e;
            }
        }
        return current;
    }

    private Object convertValue(Object value, Class<?> targetType) {
        if (value == null) {
            if (targetType.isPrimitive()) {
                throw new IllegalArgumentException("A value is required for " + targetType.getSimpleName());
            }
            return null;
        }
        if (targetType.isInstance(value)) {
            return value;
        }
        return objectMapper.convertValue(value, targetType);
    }

    private Throwable unwrapInvocationException(Throwable error) {
        Throwable current = error;
        while ((current instanceof InvocationTargetException || current instanceof ExecutionException)
                && current.getCause() != null) {
            current = current.getCause();
        }
        return current;
    }

    private void completeFinishContext(Finish finish, String instructionId, String nestId) {
        if (finish == null) {
            return;
        }
        if (isBlank(finish.getInstructionId())) {
            finish.setInstructionId(instructionId);
        }
        if (isBlank(finish.getNestId())) {
            finish.setNestId(nestId);
        }
    }

    private boolean isBlank(String value) {
        return value == null || value.trim().isEmpty();
    }

    private void scanMethods(RegisterInfo registerInfo) {
        List<EquipmentFunction> functions = new ArrayList<>();
        List<EquipmentOperation> operations = new ArrayList<>();
        List<EquipmentSetInfo> sets = new ArrayList<>();
        List<EquipmentGetInfo> gets = new ArrayList<>();
        List<EquipmentEnterAndExit> enterAndExits = new ArrayList<>();

        Method[] methods = getClass().getMethods();

        for (Method method : methods) {
            // 扫描功能
            DeviceFunction funcAttr = method.getAnnotation(DeviceFunction.class);
            if (funcAttr != null) {
                EquipmentFunction func = new EquipmentFunction();
                func.setFunctionName(funcAttr.name());
                func.setFunctionTitleCN(funcAttr.titleCN());
                func.setFunctionTitleEN(funcAttr.titleEN());
                func.setFunctionDescription(funcAttr.description());
                func.setFunctionCategoryCN(funcAttr.categoryCN());
                func.setFunctionCategoryEN(funcAttr.categoryEN());
                func.setFunctionDefaultPeriod(String.valueOf(funcAttr.defaultPeriod()));
                func.setFunctionFormJsonStructure(funcAttr.formJson());

                // 加载图标
                String iconBlack = funcAttr.iconBlack();
                if (iconBlack == null || iconBlack.isEmpty()) {
                    String iconFileBlack = funcAttr.iconFileBlack();
                    if (iconFileBlack != null && !iconFileBlack.isEmpty()) {
                        iconBlack = IconHelper.loadIcon(iconFileBlack);
                    }
                    // 如果没有指定iconFileBlack或加载失败，使用默认图标
                    if (iconBlack == null || iconBlack.isEmpty()) {
                        iconBlack = IconHelper.getDefaultFunctionIconBlack();
                    }
                }
                func.setIconBlack(iconBlack);

                String iconWhite = funcAttr.iconWhite();
                if (iconWhite == null || iconWhite.isEmpty()) {
                    String iconFileWhite = funcAttr.iconFileWhite();
                    if (iconFileWhite != null && !iconFileWhite.isEmpty()) {
                        iconWhite = IconHelper.loadIcon(iconFileWhite);
                    }
                    // 如果没有指定iconFileWhite或加载失败，使用默认图标
                    if (iconWhite == null || iconWhite.isEmpty()) {
                        iconWhite = IconHelper.getDefaultFunctionIconWhite();
                    }
                }
                func.setIconWhite(iconWhite);

                functions.add(func);
            }

            // 扫描操作
            DeviceOperation opAttr = method.getAnnotation(DeviceOperation.class);
            if (opAttr != null) {
                EquipmentOperation op = new EquipmentOperation();
                op.setOperationName(opAttr.name());
                op.setOperationTitleCN(opAttr.titleCN());
                op.setOperationTitleEN(opAttr.titleEN());
                op.setOperationDescription(opAttr.description());
                op.setOperationFormJsonStructure(opAttr.formJson());
                operations.add(op);
            }

            // 扫描设置
            DeviceSet setAttr = method.getAnnotation(DeviceSet.class);
            if (setAttr != null) {
                EquipmentSetInfo set = new EquipmentSetInfo();
                set.setSetName(setAttr.name());
                set.setSetTitleCN(setAttr.titleCN());
                set.setSetTitleEN(setAttr.titleEN());
                set.setSetType(setAttr.type());
                set.setSetUnit(setAttr.unit());
                set.setSetDescription(setAttr.description());
                sets.add(set);
            }

            // 扫描获取
            DeviceGet getAttr = method.getAnnotation(DeviceGet.class);
            if (getAttr != null) {
                EquipmentGetInfo get = new EquipmentGetInfo();
                get.setGetName(getAttr.name());
                get.setGetTitleCN(getAttr.titleCN());
                get.setGetTitleEN(getAttr.titleEN());
                get.setGetType(getAttr.type());
                get.setGetUnit(getAttr.unit());
                get.setGetDescription(getAttr.description());
                gets.add(get);
            }

            // 扫描进出
            DeviceEnterExit eeAttr = method.getAnnotation(DeviceEnterExit.class);
            if (eeAttr != null) {
                EquipmentEnterAndExit ee = new EquipmentEnterAndExit();
                ee.setEnterAndExitName(eeAttr.name());
                ee.setEnterAndExitTitleCN(eeAttr.titleCN());
                ee.setEnterAndExitTitleEN(eeAttr.titleEN());
                enterAndExits.add(ee);
            }
        }

        RegisterInfo.AdvancedInfo advancedInfo = registerInfo.getAdvancedInfo();
        if (advancedInfo == null) {
            advancedInfo = new RegisterInfo.AdvancedInfo();
            registerInfo.setAdvancedInfo(advancedInfo);
        }
        advancedInfo.setEquipmentFunctions(functions);
        advancedInfo.setEquipmentOperations(operations);
        advancedInfo.setEquipmentSetInfos(sets);
        advancedInfo.setEquipmentGetInfos(gets);
        advancedInfo.setEquipmentEnterAndExit(enterAndExits.isEmpty() ? null : enterAndExits.get(0));
    }

    private void scanFields(RegisterInfo registerInfo) {
        List<EquipmentNest> nests = new ArrayList<>();

        // 扫描带有 @DeviceNest 注解的方法（getter方法返回 EquipmentNest 对象）
        Method[] methods = getClass().getMethods();
        List<AbstractMap.SimpleEntry<Method, Integer>> nestMethodsWithOrder = new ArrayList<>();

        for (Method method : methods) {
            DeviceNest nestAttr = method.getAnnotation(DeviceNest.class);
            if (nestAttr != null) {
                nestMethodsWithOrder.add(new AbstractMap.SimpleEntry<>(method, nestAttr.order()));
            }
        }

        // 按 order 排序
        nestMethodsWithOrder.sort(Comparator.comparingInt(AbstractMap.SimpleEntry::getValue));

        // 调用方法获取 EquipmentNest 对象
        for (AbstractMap.SimpleEntry<Method, Integer> entry : nestMethodsWithOrder) {
            Method method = entry.getKey();
            try {
                Object result = method.invoke(this);
                if (result instanceof EquipmentNest) {
                    nests.add((EquipmentNest) result);
                }
            } catch (Exception e) {
                logger.error("Failed to get nest from method: {}", method.getName(), e);
            }
        }

        Collection<EquipmentNest> dynamicNests = getDynamicEquipmentNests();
        if (dynamicNests != null) {
            for (EquipmentNest nest : dynamicNests) if (nest != null) nests.add(nest);
        }

        RegisterInfo.AdvancedInfo advancedInfo = registerInfo.getAdvancedInfo();
        if (advancedInfo == null) {
            advancedInfo = new RegisterInfo.AdvancedInfo();
            registerInfo.setAdvancedInfo(advancedInfo);
        }
        advancedInfo.setEquipmentNests(nests);
    }

    /** 由配置或设备发现生成的运行时位置。 */
    protected Collection<EquipmentNest> getDynamicEquipmentNests() {
        return Collections.emptyList();
    }

    @Override
    public void close() {
        disconnect();
        if (connectionManager != null) {
            connectionManager.close();
            connectionManager = null;
            communication = null;
        }
        if (communication instanceof AutoCloseable) {
            try {
                ((AutoCloseable) communication).close();
            } catch (Exception e) {
                logger.warn("Failed to close communication", e);
            }
        }
    }
}

package com.ciai.controller.sdk.core;

import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * 设备配置类
 */
public class DeviceConfiguration {

    private static final ObjectMapper SETTINGS_MAPPER = new ObjectMapper()
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    private String deviceId;
    private CommunicationType communicationType;
    private String host;
    private int port;
    private String baseUrl;
    private String serialPort;
    private int baudRate = 9600;
    private int dataBits = 8;
    private double stopBits = 1;
    private String parity = "none";
    private String encoding = "utf-8";
    private String flowControl = "none";
    private boolean dtrEnable;
    private boolean rtsEnable;
    private boolean discardInputBeforeWrite;
    private int connectionTimeout = 5000;
    private int readTimeout = 10000;
    private int writeTimeout = 10000;
    private int deviceCallResources = 1;
    private int deviceCallTimeout = 30000;
    private Map<String, ConnectionConfiguration> connections = new LinkedHashMap<>();
    private Map<String, Object> extraSettings = new LinkedHashMap<>();

    public DeviceConfiguration() {
    }

    public DeviceConfiguration(String deviceId, CommunicationType communicationType) {
        this.deviceId = deviceId;
        this.communicationType = communicationType;
    }

    // Static factory methods
    public static DeviceConfiguration createTcp(String deviceId, String host, int port) {
        return createTcp(deviceId, host, port, 5000);
    }

    public static DeviceConfiguration createTcp(String deviceId, String host, int port, int timeout) {
        DeviceConfiguration config = new DeviceConfiguration(deviceId, CommunicationType.TCP);
        config.setHost(host);
        config.setPort(port);
        config.setConnectionTimeout(timeout);
        config.setReadTimeout(timeout);
        config.setWriteTimeout(timeout);
        return config;
    }

    public static DeviceConfiguration createHttp(String deviceId, String baseUrl) {
        return createHttp(deviceId, baseUrl, 30000);
    }

    public static DeviceConfiguration createHttp(String deviceId, String baseUrl, int timeout) {
        DeviceConfiguration config = new DeviceConfiguration(deviceId, CommunicationType.HTTP);
        config.setBaseUrl(baseUrl);
        config.setConnectionTimeout(timeout);
        return config;
    }

    public static DeviceConfiguration createSerial(String deviceId, String serialPort) {
        return createSerial(deviceId, serialPort, 9600);
    }

    public static DeviceConfiguration createSerial(String deviceId, String serialPort, int baudRate) {
        DeviceConfiguration config = new DeviceConfiguration(deviceId, CommunicationType.SERIAL);
        config.setSerialPort(serialPort);
        config.setBaudRate(baudRate);
        config.setReadTimeout(5000);
        config.setWriteTimeout(5000);
        return config;
    }

    public static DeviceConfiguration createDll(String deviceId) {
        return new DeviceConfiguration(deviceId, CommunicationType.DLL);
    }

    // Getters and Setters
    public String getDeviceId() {
        return deviceId;
    }

    public void setDeviceId(String deviceId) {
        this.deviceId = deviceId;
    }

    public CommunicationType getCommunicationType() {
        return communicationType;
    }

    public void setCommunicationType(CommunicationType communicationType) {
        this.communicationType = communicationType;
    }

    public String getHost() {
        return host;
    }

    public void setHost(String host) {
        this.host = host;
    }

    public int getPort() {
        return port;
    }

    public void setPort(int port) {
        this.port = port;
    }

    public String getBaseUrl() {
        return baseUrl;
    }

    public void setBaseUrl(String baseUrl) {
        this.baseUrl = baseUrl;
    }

    public String getSerialPort() {
        return serialPort;
    }

    public void setSerialPort(String serialPort) {
        this.serialPort = serialPort;
    }

    public int getBaudRate() {
        return baudRate;
    }

    public void setBaudRate(int baudRate) {
        this.baudRate = baudRate;
    }

    public int getDataBits() {
        return dataBits;
    }

    public void setDataBits(int dataBits) {
        this.dataBits = dataBits;
    }

    public double getStopBits() {
        return stopBits;
    }

    public void setStopBits(double stopBits) {
        this.stopBits = stopBits;
    }

    public String getParity() {
        return parity;
    }

    public void setParity(String parity) {
        this.parity = parity;
    }

    public String getEncoding() {
        return encoding;
    }

    public void setEncoding(String encoding) {
        this.encoding = encoding;
    }

    public String getFlowControl() { return flowControl; }
    public void setFlowControl(String value) { flowControl = value; }
    public boolean isDtrEnable() { return dtrEnable; }
    public void setDtrEnable(boolean value) { dtrEnable = value; }
    public boolean isRtsEnable() { return rtsEnable; }
    public void setRtsEnable(boolean value) { rtsEnable = value; }
    public boolean isDiscardInputBeforeWrite() { return discardInputBeforeWrite; }
    public void setDiscardInputBeforeWrite(boolean value) { discardInputBeforeWrite = value; }

    public int getConnectionTimeout() {
        return connectionTimeout;
    }

    public void setConnectionTimeout(int connectionTimeout) {
        this.connectionTimeout = connectionTimeout;
    }

    public int getReadWriteTimeout() {
        return Math.max(readTimeout, writeTimeout);
    }

    public void setReadWriteTimeout(int readWriteTimeout) {
        this.readTimeout = readWriteTimeout;
        this.writeTimeout = readWriteTimeout;
    }

    public int getReadTimeout() {
        return readTimeout;
    }

    public void setReadTimeout(int readTimeout) {
        this.readTimeout = readTimeout;
    }

    public int getWriteTimeout() {
        return writeTimeout;
    }

    public void setWriteTimeout(int writeTimeout) {
        this.writeTimeout = writeTimeout;
    }

    public int getDeviceCallResources() {
        return deviceCallResources;
    }

    public void setDeviceCallResources(int deviceCallResources) {
        this.deviceCallResources = deviceCallResources;
    }

    public int getDeviceCallTimeout() {
        return deviceCallTimeout;
    }

    public void setDeviceCallTimeout(int deviceCallTimeout) {
        this.deviceCallTimeout = deviceCallTimeout;
    }

    public Map<String, ConnectionConfiguration> getConnections() { return connections; }
    public void setConnections(Map<String, ConnectionConfiguration> value) {
        connections = value == null ? new LinkedHashMap<String, ConnectionConfiguration>() : value;
    }

    public Map<String, Object> getExtraSettings() {
        return extraSettings;
    }

    public void setExtraSettings(Map<String, Object> extraSettings) {
        this.extraSettings = extraSettings;
    }

    public Object getExtraSetting(String key) {
        return findSetting(key);
    }

    public <T> T getExtraSetting(String key, Class<T> targetType) {
        return getExtraSetting(key, targetType, null);
    }

    public <T> T getExtraSetting(String key, Class<T> targetType, T defaultValue) {
        Object value = findSetting(key);
        if (value == null) {
            return defaultValue;
        }
        try {
            return SETTINGS_MAPPER.convertValue(value, targetType);
        } catch (IllegalArgumentException e) {
            throw new IllegalArgumentException(
                    "Device setting '" + key + "' cannot be converted to "
                            + targetType.getSimpleName(), e);
        }
    }

    public <T> T getRequiredExtraSetting(String key, Class<T> targetType) {
        Object value = findSetting(key);
        if (value == null) {
            throw new IllegalArgumentException("Missing required setting: device.settings." + key);
        }
        try {
            return SETTINGS_MAPPER.convertValue(value, targetType);
        } catch (IllegalArgumentException e) {
            throw new IllegalArgumentException("Device setting '" + key + "' cannot be converted to "
                    + targetType.getSimpleName(), e);
        }
    }

    @SuppressWarnings("unchecked")
    private Object findSetting(String key) {
        if (key == null || key.trim().isEmpty()) return null;
        Object current = extraSettings;
        for (String part : key.split("\\.")) {
            if (!(current instanceof Map)) return null;
            Map<String, Object> map = (Map<String, Object>) current;
            Object next = null;
            for (Map.Entry<String, Object> entry : map.entrySet()) {
                if (entry.getKey().equalsIgnoreCase(part)) { next = entry.getValue(); break; }
            }
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    public void setExtraSetting(String key, Object value) {
        extraSettings.put(key, value);
    }
}

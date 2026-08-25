package com.ciai.controller.sdk.config;

import com.ciai.controller.sdk.core.ConnectionConfiguration;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * 驱动配置模型
 */
public class DriverConfig {

    private ServerConfig server;
    private CallbackConfig callback;
    private DeviceConfigSection device;
    private transient String sourceDirectory;

    public String getSourceDirectory() {
        return sourceDirectory;
    }

    void setSourceDirectory(String sourceDirectory) {
        this.sourceDirectory = sourceDirectory;
    }

    public ServerConfig getServer() {
        return server;
    }

    public void setServer(ServerConfig server) {
        this.server = server;
    }

    public CallbackConfig getCallback() {
        return callback;
    }

    public void setCallback(CallbackConfig callback) {
        this.callback = callback;
    }

    public DeviceConfigSection getDevice() {
        return device;
    }

    public void setDevice(DeviceConfigSection device) {
        this.device = device;
    }

    /**
     * 服务器配置
     * 参照 IncubatorController 的 Spring Boot SSL 配置
     */
    public static class ServerConfig {
        private int port = 8080;
        private String host = "localhost";
        private boolean useHttps = false;
        private CertificateConfig certificate;
        private TrustStoreConfig trustStore;  // 信任库配置（新增）
        private ClientAuthConfig clientAuth;
        private SslConfig ssl;  // SSL/TLS 配置（新增）
        private int maxConcurrentRequests = 100;
        private int maxRequestBodyBytes = 1024 * 1024;
        private int functionQueueCapacity = 100;
        private int idempotencyCapacity = 10000;
        private int shutdownTimeoutMs = 30000;
        private boolean allowReplaceCertificateBinding;

        public int getPort() {
            return port;
        }

        public void setPort(int port) {
            this.port = port;
        }

        public String getHost() {
            return host;
        }

        public void setHost(String host) {
            this.host = host;
        }

        public boolean isUseHttps() {
            return useHttps;
        }

        public void setUseHttps(boolean useHttps) {
            this.useHttps = useHttps;
        }

        public CertificateConfig getCertificate() {
            return certificate;
        }

        public void setCertificate(CertificateConfig certificate) {
            this.certificate = certificate;
        }

        public TrustStoreConfig getTrustStore() {
            return trustStore;
        }

        public void setTrustStore(TrustStoreConfig trustStore) {
            this.trustStore = trustStore;
        }

        public ClientAuthConfig getClientAuth() {
            return clientAuth;
        }

        public void setClientAuth(ClientAuthConfig clientAuth) {
            this.clientAuth = clientAuth;
        }

        public SslConfig getSsl() {
            return ssl;
        }

        public void setSsl(SslConfig ssl) {
            this.ssl = ssl;
        }

        public int getMaxConcurrentRequests() { return maxConcurrentRequests; }
        public void setMaxConcurrentRequests(int value) { this.maxConcurrentRequests = value; }
        public int getMaxRequestBodyBytes() { return maxRequestBodyBytes; }
        public void setMaxRequestBodyBytes(int value) { this.maxRequestBodyBytes = value; }
        public int getFunctionQueueCapacity() { return functionQueueCapacity; }
        public void setFunctionQueueCapacity(int value) { this.functionQueueCapacity = value; }
        public int getIdempotencyCapacity() { return idempotencyCapacity; }
        public void setIdempotencyCapacity(int value) { this.idempotencyCapacity = value; }
        public int getShutdownTimeoutMs() { return shutdownTimeoutMs; }
        public void setShutdownTimeoutMs(int value) { this.shutdownTimeoutMs = value; }
        public boolean isAllowReplaceCertificateBinding() { return allowReplaceCertificateBinding; }
        public void setAllowReplaceCertificateBinding(boolean value) { this.allowReplaceCertificateBinding = value; }
    }

    /**
     * 证书配置
     * 对应 Spring Boot: server.ssl.key-store-*
     */
    public static class CertificateConfig {
        private String path;
        private String password;
        private String type = "PKCS12";  // 密钥库类型
        private String alias;  // 密钥别名

        public String getPath() {
            return path;
        }

        public void setPath(String path) {
            this.path = path;
        }

        public String getPassword() {
            return password;
        }

        public void setPassword(String password) {
            this.password = password;
        }

        public String getType() {
            return type;
        }

        public void setType(String type) {
            this.type = type;
        }

        public String getAlias() {
            return alias;
        }

        public void setAlias(String alias) {
            this.alias = alias;
        }
    }

    /**
     * 信任库配置
     * 对应 Spring Boot: server.ssl.trust-store-*
     */
    public static class TrustStoreConfig {
        private String path;
        private String password;
        private String type = "PKCS12";

        public String getPath() {
            return path;
        }

        public void setPath(String path) {
            this.path = path;
        }

        public String getPassword() {
            return password;
        }

        public void setPassword(String password) {
            this.password = password;
        }

        public String getType() {
            return type;
        }

        public void setType(String type) {
            this.type = type;
        }
    }

    /**
     * SSL/TLS 配置
     * 对应 Spring Boot: server.ssl.protocol, server.ssl.ciphers, server.ssl.enabled-protocols
     */
    public static class SslConfig {
        private String protocol = "TLSv1.2";  // 对应 server.ssl.protocol
        private List<String> enabledProtocols;  // 对应 server.ssl.enabled-protocols
        private List<String> ciphers;  // 对应 server.ssl.ciphers

        public String getProtocol() {
            return protocol;
        }

        public void setProtocol(String protocol) {
            this.protocol = protocol;
        }

        public List<String> getEnabledProtocols() {
            return enabledProtocols;
        }

        public void setEnabledProtocols(List<String> enabledProtocols) {
            this.enabledProtocols = enabledProtocols;
        }

        public List<String> getCiphers() {
            return ciphers;
        }

        public void setCiphers(List<String> ciphers) {
            this.ciphers = ciphers;
        }
    }

    /**
     * 客户端认证配置
     * 对应 Spring Boot: server.ssl.client-auth
     */
    public static class ClientAuthConfig {
        private String mode = "need";  // need, want, none（对应 Spring Boot: server.ssl.client-auth）
        private boolean enabled = true;
        private List<String> trustedThumbprints;

        public String getMode() {
            return mode;
        }

        public void setMode(String mode) {
            this.mode = mode;
        }

        public boolean isEnabled() {
            return enabled;
        }

        public void setEnabled(boolean enabled) {
            this.enabled = enabled;
        }

        public List<String> getTrustedThumbprints() {
            return trustedThumbprints;
        }

        public void setTrustedThumbprints(List<String> trustedThumbprints) {
            this.trustedThumbprints = trustedThumbprints;
        }
    }

    /**
     * 回调配置
     */
    public static class CallbackConfig {
        private String url;
        private int timeoutMs = 30000;
        private boolean enabled = true;

        public String getUrl() {
            return url;
        }

        public void setUrl(String url) {
            this.url = url;
        }

        public int getTimeoutMs() {
            return timeoutMs;
        }

        public void setTimeoutMs(int timeoutMs) {
            this.timeoutMs = timeoutMs;
        }

        public boolean isEnabled() {
            return enabled;
        }

        public void setEnabled(boolean enabled) {
            this.enabled = enabled;
        }
    }

    /**
     * 设备配置部分
     */
    public static class DeviceConfigSection {
        private String deviceId;
        private String communicationType;
        private int deviceCallResources = 1;
        private int deviceCallTimeoutMs = 30000;
        private TcpConfig tcp;
        private HttpConfig http;
        private SerialConfig serial;
        private Map<String, ConnectionConfiguration> connections = new LinkedHashMap<>();
        private Map<String, Object> settings = new LinkedHashMap<>();

        public String getDeviceId() {
            return deviceId;
        }

        public void setDeviceId(String deviceId) {
            this.deviceId = deviceId;
        }

        public String getCommunicationType() {
            return communicationType;
        }

        public void setCommunicationType(String communicationType) {
            this.communicationType = communicationType;
        }

        public int getDeviceCallResources() {
            return deviceCallResources;
        }

        public void setDeviceCallResources(int deviceCallResources) {
            this.deviceCallResources = deviceCallResources;
        }

        public int getDeviceCallTimeoutMs() {
            return deviceCallTimeoutMs;
        }

        public void setDeviceCallTimeoutMs(int deviceCallTimeoutMs) {
            this.deviceCallTimeoutMs = deviceCallTimeoutMs;
        }

        public TcpConfig getTcp() {
            return tcp;
        }

        public void setTcp(TcpConfig tcp) {
            this.tcp = tcp;
        }

        public HttpConfig getHttp() {
            return http;
        }

        public void setHttp(HttpConfig http) {
            this.http = http;
        }

        public SerialConfig getSerial() {
            return serial;
        }

        public void setSerial(SerialConfig serial) {
            this.serial = serial;
        }

        public Map<String, ConnectionConfiguration> getConnections() { return connections; }
        public void setConnections(Map<String, ConnectionConfiguration> value) {
            connections = value == null ? new LinkedHashMap<String, ConnectionConfiguration>() : value;
        }

        public Map<String, Object> getSettings() {
            return settings;
        }

        public void setSettings(Map<String, Object> settings) {
            this.settings = settings == null ? new LinkedHashMap<>() : settings;
        }
    }

    /**
     * TCP配置
     */
    public static class TcpConfig {
        private String host;
        private int port;
        private int timeoutMs = 5000;
        private Integer connectTimeoutMs;
        private Integer readTimeoutMs;
        private Integer writeTimeoutMs;

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

        public int getTimeoutMs() {
            return timeoutMs;
        }

        public void setTimeoutMs(int timeoutMs) {
            this.timeoutMs = timeoutMs;
        }

        public Integer getConnectTimeoutMs() { return connectTimeoutMs; }
        public void setConnectTimeoutMs(Integer value) { this.connectTimeoutMs = value; }
        public Integer getReadTimeoutMs() { return readTimeoutMs; }
        public void setReadTimeoutMs(Integer value) { this.readTimeoutMs = value; }
        public Integer getWriteTimeoutMs() { return writeTimeoutMs; }
        public void setWriteTimeoutMs(Integer value) { this.writeTimeoutMs = value; }
    }

    /**
     * HTTP配置
     */
    public static class HttpConfig {
        private String baseUrl;
        private int timeoutMs = 30000;

        public String getBaseUrl() {
            return baseUrl;
        }

        public void setBaseUrl(String baseUrl) {
            this.baseUrl = baseUrl;
        }

        public int getTimeoutMs() {
            return timeoutMs;
        }

        public void setTimeoutMs(int timeoutMs) {
            this.timeoutMs = timeoutMs;
        }
    }

    /**
     * 串口配置
     */
    public static class SerialConfig {
        private String port;
        private int baudRate = 9600;
        private int dataBits = 8;
        private double stopBits = 1;
        private String parity = "none";
        private int timeoutMs = 5000;
        private Integer readTimeoutMs;
        private Integer writeTimeoutMs;
        private String encoding = "utf-8";
        private String flowControl = "none";
        private boolean dtrEnable;
        private boolean rtsEnable;
        private boolean discardInputBeforeWrite;

        public String getPort() {
            return port;
        }

        public void setPort(String port) {
            this.port = port;
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

        public int getTimeoutMs() { return timeoutMs; }
        public void setTimeoutMs(int timeoutMs) { this.timeoutMs = timeoutMs; }
        public Integer getReadTimeoutMs() { return readTimeoutMs; }
        public void setReadTimeoutMs(Integer value) { this.readTimeoutMs = value; }
        public Integer getWriteTimeoutMs() { return writeTimeoutMs; }
        public void setWriteTimeoutMs(Integer value) { this.writeTimeoutMs = value; }
        public String getEncoding() { return encoding; }
        public void setEncoding(String encoding) { this.encoding = encoding; }
        public String getFlowControl() { return flowControl; }
        public void setFlowControl(String value) { flowControl = value; }
        public boolean isDtrEnable() { return dtrEnable; }
        public void setDtrEnable(boolean value) { dtrEnable = value; }
        public boolean isRtsEnable() { return rtsEnable; }
        public void setRtsEnable(boolean value) { rtsEnable = value; }
        public boolean isDiscardInputBeforeWrite() { return discardInputBeforeWrite; }
        public void setDiscardInputBeforeWrite(boolean value) { discardInputBeforeWrite = value; }
    }
}

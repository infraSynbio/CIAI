package com.ciai.controller.sdk.webserver;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * HTTPS服务器配置选项
 * 参照 IncubatorController 的 Spring Boot SSL 配置实现
 */
public class HttpsOptions {

    // 基本服务器配置
    private int port = 443;
    private String host = "localhost";
    private boolean useHttps = true;

    // 服务端证书配置 (对应 Spring Boot: server.ssl.key-store-*)
    private String serverCertificatePath;
    private String serverCertificatePassword;
    private String keyAlias;  // 密钥别名 (对应 Spring Boot: server.ssl.key-alias)
    private String keyStoreType = "PKCS12";  // 密钥库类型 (对应 Spring Boot: server.ssl.key-store-type)

    // 信任库配置 (对应 Spring Boot: server.ssl.trust-store-*)
    private String trustStorePath;
    private String trustStorePassword;
    private String trustStoreType = "PKCS12";  // 信任库类型

    // TLS 协议配置 (对应 Spring Boot: server.ssl.protocol, server.ssl.enabled-protocols)
    private String protocol = "TLSv1.2";  // Java 8与现代运行时共同支持的安全基线
    private String[] enabledProtocols = new String[]{"TLSv1.2"};  // 启用的协议

    // 加密套件配置 (对应 Spring Boot: server.ssl.ciphers)
    private String[] ciphers = new String[0]; // 空数组表示使用JVM安全策略

    // 客户端认证配置 (对应 Spring Boot: server.ssl.client-auth)
    private ClientAuthMode clientAuth = ClientAuthMode.NEED;  // need, want, none
    private boolean requireClientCertificate = true;
    private String[] trustedClientThumbprints = new String[0];
    private String[] trustedIssuerThumbprints = new String[0];

    // 回调配置
    private String callbackUrl;
    private int callbackTimeoutMs = 30000;
    private boolean enableCallback = true;
    private int maxConcurrentRequests = 100;
    private int maxRequestBodyBytes = 1024 * 1024;
    private int functionQueueCapacity = 100;
    private int idempotencyCapacity = 10000;
    private int shutdownTimeoutMs = 30000;

    /**
     * 客户端认证模式
     * 对应 Spring Boot: server.ssl.client-auth
     */
    public enum ClientAuthMode {
        NEED,    // 需要客户端证书
        WANT,    // 想要客户端证书，但不是必须
        NONE     // 不需要客户端证书
    }

    public HttpsOptions() {
    }

    // Getters and Setters
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

    public String getServerCertificatePath() {
        return serverCertificatePath;
    }

    public void setServerCertificatePath(String serverCertificatePath) {
        this.serverCertificatePath = serverCertificatePath;
    }

    public String getServerCertificatePassword() {
        return serverCertificatePassword;
    }

    public void setServerCertificatePassword(String serverCertificatePassword) {
        this.serverCertificatePassword = serverCertificatePassword;
    }

    public boolean isRequireClientCertificate() {
        return requireClientCertificate;
    }

    public void setRequireClientCertificate(boolean requireClientCertificate) {
        this.requireClientCertificate = requireClientCertificate;
    }

    public String[] getTrustedClientThumbprints() {
        return trustedClientThumbprints;
    }

    public void setTrustedClientThumbprints(String[] trustedClientThumbprints) {
        this.trustedClientThumbprints = trustedClientThumbprints;
    }

    public String[] getTrustedIssuerThumbprints() {
        return trustedIssuerThumbprints;
    }

    public void setTrustedIssuerThumbprints(String[] trustedIssuerThumbprints) {
        this.trustedIssuerThumbprints = trustedIssuerThumbprints;
    }

    public String getCallbackUrl() {
        return callbackUrl;
    }

    public void setCallbackUrl(String callbackUrl) {
        this.callbackUrl = callbackUrl;
    }

    public int getCallbackTimeoutMs() {
        return callbackTimeoutMs;
    }

    public void setCallbackTimeoutMs(int callbackTimeoutMs) {
        this.callbackTimeoutMs = callbackTimeoutMs;
    }

    public boolean isEnableCallback() {
        return enableCallback;
    }

    public void setEnableCallback(boolean enableCallback) {
        this.enableCallback = enableCallback;
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

    // ========== 新增的配置项 Getter/Setter ==========

    public String getKeyAlias() {
        return keyAlias;
    }

    public void setKeyAlias(String keyAlias) {
        this.keyAlias = keyAlias;
    }

    public String getKeyStoreType() {
        return keyStoreType;
    }

    public void setKeyStoreType(String keyStoreType) {
        this.keyStoreType = keyStoreType;
    }

    public String getTrustStorePath() {
        return trustStorePath;
    }

    public void setTrustStorePath(String trustStorePath) {
        this.trustStorePath = trustStorePath;
    }

    public String getTrustStorePassword() {
        return trustStorePassword;
    }

    public void setTrustStorePassword(String trustStorePassword) {
        this.trustStorePassword = trustStorePassword;
    }

    public String getTrustStoreType() {
        return trustStoreType;
    }

    public void setTrustStoreType(String trustStoreType) {
        this.trustStoreType = trustStoreType;
    }

    public String getProtocol() {
        return protocol;
    }

    public void setProtocol(String protocol) {
        this.protocol = protocol;
    }

    public String[] getEnabledProtocols() {
        return enabledProtocols;
    }

    public void setEnabledProtocols(String[] enabledProtocols) {
        this.enabledProtocols = enabledProtocols;
    }

    public String[] getCiphers() {
        return ciphers;
    }

    public void setCiphers(String[] ciphers) {
        this.ciphers = ciphers;
    }

    public ClientAuthMode getClientAuth() {
        return clientAuth;
    }

    public void setClientAuth(ClientAuthMode clientAuth) {
        this.clientAuth = clientAuth;
        // 同步更新 requireClientCertificate
        this.requireClientCertificate = (clientAuth == ClientAuthMode.NEED || clientAuth == ClientAuthMode.WANT);
    }

    /**
     * 获取监听前缀
     */
    public String getListenPrefix() {
        String scheme = useHttps ? "https" : "http";
        return scheme + "://" + host + ":" + port + "/";
    }

    /**
     * 验证配置
     */
    public void validate() {
        List<String> errors = new ArrayList<>();

        if (port <= 0 || port > 65535) {
            errors.add("Invalid port: " + port);
        }
        if (maxConcurrentRequests <= 0 || maxRequestBodyBytes <= 0
                || functionQueueCapacity <= 0 || idempotencyCapacity <= 0
                || shutdownTimeoutMs <= 0) {
            errors.add("Server concurrency, body, queue, idempotency and shutdown limits must be positive");
        }

        if (useHttps) {
            if (serverCertificatePath == null || serverCertificatePath.isEmpty()) {
                errors.add("Server certificate path (key-store) is required for HTTPS");
            }

            // 如果需要客户端证书验证，检查信任库配置
            if (clientAuth == ClientAuthMode.NEED || clientAuth == ClientAuthMode.WANT) {
                // 信任库路径可以为空，此时使用密钥库作为信任库（与 IncubatorController 一致）
                // 但如果配置了信任库路径，则必须有密码
                if (trustStorePath != null && !trustStorePath.isEmpty()) {
                    // 信任库已配置，OK
                }
                // 如果没有配置信任库，则使用密钥库作为信任库（与 IncubatorController 的做法一致）
            }
        }

        if (!errors.isEmpty()) {
            throw new IllegalArgumentException("Configuration errors: " + String.join(", ", errors));
        }
    }

    /**
     * 创建HTTP配置
     */
    public static HttpsOptions createHttp(int port) {
        return createHttp(port, "localhost");
    }

    /**
     * 创建HTTP配置
     */
    public static HttpsOptions createHttp(int port, String host) {
        HttpsOptions options = new HttpsOptions();
        options.setPort(port);
        options.setHost(host);
        options.setUseHttps(false);
        options.setRequireClientCertificate(false);
        return options;
    }

    /**
     * 创建HTTPS配置
     */
    public static HttpsOptions createHttps(int port, String host, String certPath, String certPassword) {
        HttpsOptions options = new HttpsOptions();
        options.setPort(port);
        options.setHost(host);
        options.setUseHttps(true);
        options.setServerCertificatePath(certPath);
        options.setServerCertificatePassword(certPassword);
        return options;
    }

    /**
     * 创建完整的HTTPS配置（参照 IncubatorController 的 Spring Boot 配置）
     * @param port 端口
     * @param host 主机
     * @param keyStorePath 密钥库路径 (server.ssl.key-store)
     * @param keyStorePassword 密钥库密码 (server.ssl.key-store-password)
     * @param keyStoreType 密钥库类型 (server.ssl.key-store-type)
     * @param keyAlias 密钥别名 (server.ssl.key-alias)
     * @param trustStorePath 信任库路径 (server.ssl.trust-store)，可为null则使用密钥库
     * @param trustStorePassword 信任库密码 (server.ssl.trust-store-password)
     * @param protocol SSL协议 (server.ssl.protocol)
     * @param ciphers 加密套件 (server.ssl.ciphers)
     * @param clientAuth 客户端认证模式 (server.ssl.client-auth)
     */
    public static HttpsOptions createHttpsFull(
            int port, String host,
            String keyStorePath, String keyStorePassword, String keyStoreType, String keyAlias,
            String trustStorePath, String trustStorePassword,
            String protocol, String[] ciphers,
            ClientAuthMode clientAuth) {
        HttpsOptions options = new HttpsOptions();
        options.setPort(port);
        options.setHost(host);
        options.setUseHttps(true);

        // 密钥库配置
        options.setServerCertificatePath(keyStorePath);
        options.setServerCertificatePassword(keyStorePassword);
        options.setKeyStoreType(keyStoreType != null ? keyStoreType : "PKCS12");
        options.setKeyAlias(keyAlias);

        // 信任库配置
        options.setTrustStorePath(trustStorePath);
        options.setTrustStorePassword(trustStorePassword);

        // TLS配置
        options.setProtocol(protocol != null ? protocol : "TLSv1.2");
        options.setEnabledProtocols(new String[]{options.getProtocol()});
        options.setCiphers(ciphers != null ? ciphers : new String[0]);

        // 客户端认证
        options.setClientAuth(clientAuth != null ? clientAuth : ClientAuthMode.NEED);

        return options;
    }

    @Override
    public String toString() {
        return "HttpsOptions{" +
                "port=" + port +
                ", host='" + host + '\'' +
                ", useHttps=" + useHttps +
                ", keyStoreType='" + keyStoreType + '\'' +
                ", keyAlias='" + keyAlias + '\'' +
                ", protocol='" + protocol + '\'' +
                ", enabledProtocols=" + Arrays.toString(enabledProtocols) +
                ", ciphers=" + Arrays.toString(ciphers) +
                ", clientAuth=" + clientAuth +
                ", trustStorePath='" + trustStorePath + '\'' +
                ", callbackUrl='" + callbackUrl + '\'' +
                '}';
    }
}

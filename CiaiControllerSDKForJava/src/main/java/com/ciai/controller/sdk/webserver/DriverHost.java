package com.ciai.controller.sdk.webserver;

import com.ciai.controller.sdk.config.DriverConfig;
import com.ciai.controller.sdk.config.YamlConfigLoader;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.logging.LoggerProvider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.List;

/**
 * 驱动宿主（启动器）
 */
public final class DriverHost {

    private static final Logger logger = LoggerFactory.getLogger(DriverHost.class);

    private DriverHost() {
        // 私有构造函数，防止实例化
    }

    /**
     * 从YAML配置文件启动
     */
    public static <T extends DeviceDriverBase> void run(Class<T> driverClass, String configPath) {
        run(driverClass, configPath, null);
    }

    /**
     * 从YAML配置文件启动
     */
    public static <T extends DeviceDriverBase> void run(Class<T> driverClass, String configPath,
                                                        org.slf4j.ILoggerFactory loggerFactory) {
        try {
            DriverConfig config = YamlConfigLoader.loadOrThrow(configPath);

            HttpsOptions options = YamlConfigLoader.toHttpsOptions(config);
            DeviceConfiguration deviceConfig = YamlConfigLoader.toDeviceConfiguration(config);

            run(driverClass, options, deviceConfig);
        } catch (Exception e) {
            logger.error("Failed to start driver from config: {}", configPath, e);
            throw new RuntimeException("Failed to start driver", e);
        }
    }

    /**
     * 从默认配置文件启动
     */
    public static <T extends DeviceDriverBase> void run(Class<T> driverClass) {
        run(driverClass, "application.yml");
    }

    /**
     * 从HttpsOptions启动
     */
    public static <T extends DeviceDriverBase> void run(Class<T> driverClass, HttpsOptions options,
                                                        DeviceConfiguration deviceConfig) {
        try {
            T driver = createDriver(driverClass, deviceConfig);

            // 初始化驱动
            if (!driver.initialize()) {
                throw new IllegalStateException("Failed to initialize driver");
            }

            // 创建并启动服务器
            DriverHttpServer server = new DriverHttpServer(options, driver);

            // 添加关闭钩子
            Runtime.getRuntime().addShutdownHook(new Thread(() -> {
                logger.info("Shutting down...");
                server.stop();
                driver.close();
            }));

            // 启动服务器
            server.start();

            // 保持运行
            logger.info("Driver host started. Press Ctrl+C to stop.");
            Thread.currentThread().join();

        } catch (Exception e) {
            logger.error("Failed to start driver host", e);
            throw new RuntimeException("Failed to start driver host", e);
        }
    }

    /**
     * 异步启动
     */
    public static <T extends DeviceDriverBase> DriverHttpServer runAsync(Class<T> driverClass, HttpsOptions options,
                                                                    DeviceConfiguration deviceConfig) {
        try {
            T driver = createDriver(driverClass, deviceConfig);

            if (!driver.initialize()) {
                throw new IllegalStateException("Failed to initialize driver");
            }

            DriverHttpServer server = new DriverHttpServer(options, driver);
            server.start();

            return server;
        } catch (Exception e) {
            logger.error("Failed to start driver host async", e);
            throw new RuntimeException("Failed to start driver host async", e);
        }
    }

    /**
     * 创建驱动实例
     */
    public static <T extends DeviceDriverBase> T createDriver(Class<T> driverClass, DeviceConfiguration config) {
        try {
            if (config != null) {
                try {
                    return driverClass.getConstructor(DeviceConfiguration.class).newInstance(config);
                } catch (NoSuchMethodException ignored) {
                    T driver = driverClass.getConstructor().newInstance();
                    driver.applyConfiguration(config);
                    return driver;
                }
            }
            return driverClass.getConstructor().newInstance();
        } catch (Exception e) {
            logger.error("Failed to create driver instance: {}", driverClass.getName(), e);
            throw new RuntimeException("Failed to create driver instance", e);
        }
    }

    /**
     * 创建配置构建器
     */
    public static HttpsOptionsBuilder createOptionsBuilder() {
        return new HttpsOptionsBuilder();
    }

    /**
     * HTTPS配置构建器
     * 参照 IncubatorController 的 Spring Boot SSL 配置
     */
    public static class HttpsOptionsBuilder {
        private int port = 443;
        private String host = "localhost";
        private boolean useHttps = true;
        private String serverCertificatePath;
        private String serverCertificatePassword;
        private String keyStoreType = "PKCS12";
        private String keyAlias;
        private String trustStorePath;
        private String trustStorePassword;
        private String trustStoreType = "PKCS12";
        private String protocol = "TLSv1.3";
        private String[] enabledProtocols = new String[]{"TLSv1.3"};
        private String[] ciphers = new String[]{"TLS_AES_256_GCM_SHA384", "TLS_CHACHA20_POLY1305_SHA256"};
        private HttpsOptions.ClientAuthMode clientAuth = HttpsOptions.ClientAuthMode.NEED;
        private boolean requireClientCertificate = true;
        private final List<String> trustedClientThumbprints = new ArrayList<>();
        private String callbackUrl;
        private int callbackTimeoutMs = 30000;
        private boolean enableCallback = true;

        public HttpsOptionsBuilder withPort(int port) {
            this.port = port;
            return this;
        }

        public HttpsOptionsBuilder withHost(String host) {
            this.host = host;
            return this;
        }

        public HttpsOptionsBuilder useHttps(boolean useHttps) {
            this.useHttps = useHttps;
            return this;
        }

        public HttpsOptionsBuilder useHttp() {
            this.useHttps = false;
            this.requireClientCertificate = false;
            this.clientAuth = HttpsOptions.ClientAuthMode.NONE;
            return this;
        }

        public HttpsOptionsBuilder withServerCertificate(String path, String password) {
            this.serverCertificatePath = path;
            this.serverCertificatePassword = password;
            return this;
        }

        public HttpsOptionsBuilder withServerCertificate(String path) {
            return withServerCertificate(path, null);
        }

        public HttpsOptionsBuilder withKeyStoreType(String keyStoreType) {
            this.keyStoreType = keyStoreType;
            return this;
        }

        public HttpsOptionsBuilder withKeyAlias(String keyAlias) {
            this.keyAlias = keyAlias;
            return this;
        }

        public HttpsOptionsBuilder withTrustStore(String path, String password, String type) {
            this.trustStorePath = path;
            this.trustStorePassword = password;
            this.trustStoreType = type;
            return this;
        }

        public HttpsOptionsBuilder withTrustStore(String path, String password) {
            return withTrustStore(path, password, "PKCS12");
        }

        public HttpsOptionsBuilder withProtocol(String protocol) {
            this.protocol = protocol;
            return this;
        }

        public HttpsOptionsBuilder withEnabledProtocols(String... protocols) {
            this.enabledProtocols = protocols;
            return this;
        }

        public HttpsOptionsBuilder withCiphers(String... ciphers) {
            this.ciphers = ciphers;
            return this;
        }

        public HttpsOptionsBuilder withClientAuth(HttpsOptions.ClientAuthMode clientAuth) {
            this.clientAuth = clientAuth;
            this.requireClientCertificate = clientAuth != HttpsOptions.ClientAuthMode.NONE;
            return this;
        }

        public HttpsOptionsBuilder requireClientCertificate(boolean require) {
            this.requireClientCertificate = require;
            this.clientAuth = require ? HttpsOptions.ClientAuthMode.NEED : HttpsOptions.ClientAuthMode.NONE;
            return this;
        }

        public HttpsOptionsBuilder addTrustedClientThumbprint(String... thumbprints) {
            for (String thumbprint : thumbprints) {
                this.trustedClientThumbprints.add(thumbprint);
            }
            return this;
        }

        public HttpsOptionsBuilder withCallbackUrl(String url) {
            this.callbackUrl = url;
            return this;
        }

        public HttpsOptionsBuilder withCallbackTimeout(int timeoutMs) {
            this.callbackTimeoutMs = timeoutMs;
            return this;
        }

        public HttpsOptionsBuilder enableCallback(boolean enable) {
            this.enableCallback = enable;
            return this;
        }

        public HttpsOptions build() {
            HttpsOptions options = new HttpsOptions();
            options.setPort(port);
            options.setHost(host);
            options.setUseHttps(useHttps);
            options.setServerCertificatePath(serverCertificatePath);
            options.setServerCertificatePassword(serverCertificatePassword);
            options.setKeyStoreType(keyStoreType);
            options.setKeyAlias(keyAlias);
            options.setTrustStorePath(trustStorePath);
            options.setTrustStorePassword(trustStorePassword);
            options.setTrustStoreType(trustStoreType);
            options.setProtocol(protocol);
            options.setEnabledProtocols(enabledProtocols);
            options.setCiphers(ciphers);
            options.setClientAuth(clientAuth);
            options.setRequireClientCertificate(requireClientCertificate);
            options.setTrustedClientThumbprints(trustedClientThumbprints.toArray(new String[0]));
            options.setCallbackUrl(callbackUrl);
            options.setCallbackTimeoutMs(callbackTimeoutMs);
            options.setEnableCallback(enableCallback);
            return options;
        }
    }
}

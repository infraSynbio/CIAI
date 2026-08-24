package com.ciai.controller.sdk.config;

import com.ciai.controller.sdk.core.CommunicationType;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.webserver.HttpsOptions;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.yaml.snakeyaml.Yaml;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * YAML配置加载器
 */
public class YamlConfigLoader {

    private static final Logger logger = LoggerFactory.getLogger(YamlConfigLoader.class);
    private static final Yaml yaml = new Yaml();
    private static final Pattern ENVIRONMENT_VARIABLE = Pattern.compile(
            "\\$\\{([A-Za-z_][A-Za-z0-9_]*)(?::-([^}]*))?}");
    private static final Set<String> STANDARD_DEVICE_KEYS = new HashSet<>(Arrays.asList(
            "deviceId", "communicationType", "deviceCallResources", "deviceCallTimeoutMs",
            "tcp", "http", "serial", "connections", "settings"));

    /**
     * 从文件加载配置
     */
    public static DriverConfig load(String configPath) {
        try {
            return loadOrThrow(configPath);
        } catch (Exception e) {
            logger.error("Failed to load config from: {}", configPath, e);
            return null;
        }
    }

    /**
     * 加载配置并保留具体错误。宿主应使用此入口，使文件、环境变量和YAML错误能直接反馈给用户。
     */
    public static DriverConfig loadOrThrow(String configPath) throws IOException {
        if (configPath == null || configPath.trim().isEmpty()) {
            throw new IllegalArgumentException("Config path is required");
        }

        Path path = Paths.get(configPath);
        if (Files.exists(path)) {
            DriverConfig config = parseOrThrow(new String(Files.readAllBytes(path), StandardCharsets.UTF_8));
            logger.info("Config loaded from file: {}", configPath);
            return config;
        }

        InputStream classpathStream = YamlConfigLoader.class.getResourceAsStream("/" + configPath);
        if (classpathStream != null) {
            try (InputStream inputStream = classpathStream) {
                DriverConfig config = parseOrThrow(readUtf8(inputStream));
                logger.info("Config loaded from classpath: {}", configPath);
                return config;
            }
        }

        throw new IOException("Config file not found: " + configPath);
    }

    /**
     * 从默认路径加载配置
     */
    public static DriverConfig load() {
        return load("application.yml");
    }

    /**
     * 解析YAML字符串
     */
    public static DriverConfig parse(String yamlContent) {
        try {
            return parseOrThrow(yamlContent);
        } catch (Exception e) {
            logger.error("Failed to parse YAML content", e);
            return null;
        }
    }

    /** 严格解析配置；宿主启动时优先使用，错误信息会直接指出缺失的环境变量或YAML问题。 */
    @SuppressWarnings("unchecked")
    public static DriverConfig parseOrThrow(String yamlContent) {
        if (yamlContent == null || yamlContent.trim().isEmpty()) {
            throw new IllegalArgumentException("YAML configuration is empty");
        }

        String expanded = expandEnvironmentVariables(yamlContent);
        Object rootObject = yaml.load(expanded);
        if (!(rootObject instanceof Map)) {
            throw new IllegalArgumentException("YAML root must be an object");
        }

        // 兼容旧驱动把自定义字段直接写在device下的方式，并统一归入settings。
        Map<String, Object> root = (Map<String, Object>) rootObject;
        Object deviceObject = root.get("device");
        if (deviceObject instanceof Map) {
            Map<String, Object> device = (Map<String, Object>) deviceObject;
            Object settingsObject = device.get("settings");
            Map<String, Object> settings = settingsObject instanceof Map
                    ? new LinkedHashMap<>((Map<String, Object>) settingsObject)
                    : new LinkedHashMap<>();
            for (String key : new ArrayList<>(device.keySet())) {
                if (!STANDARD_DEVICE_KEYS.contains(key)) {
                    settings.putIfAbsent(key, device.remove(key));
                }
            }
            device.put("settings", settings);
        }

        return yaml.loadAs(yaml.dump(root), DriverConfig.class);
    }

    private static String expandEnvironmentVariables(String content) {
        String[] lines = content.split("\n", -1);
        StringBuilder expanded = new StringBuilder(content.length());
        for (int index = 0; index < lines.length; index++) {
            String line = lines[index];
            int commentIndex = findYamlCommentIndex(line);
            String yaml = commentIndex < 0 ? line : line.substring(0, commentIndex);
            String comment = commentIndex < 0 ? "" : line.substring(commentIndex);
            expanded.append(expandEnvironmentVariablesInYaml(yaml)).append(comment);
            if (index + 1 < lines.length) {
                expanded.append('\n');
            }
        }
        return expanded.toString();
    }

    private static String expandEnvironmentVariablesInYaml(String yaml) {
        Matcher matcher = ENVIRONMENT_VARIABLE.matcher(yaml);
        StringBuffer result = new StringBuffer();
        while (matcher.find()) {
            String value = System.getenv(matcher.group(1));
            if (value == null) {
                value = matcher.group(2);
            }
            if (value == null) {
                throw new IllegalArgumentException(
                        "Required environment variable is not set: " + matcher.group(1));
            }
            matcher.appendReplacement(result, Matcher.quoteReplacement(value));
        }
        matcher.appendTail(result);
        return result.toString();
    }

    private static int findYamlCommentIndex(String line) {
        boolean inSingleQuote = false;
        boolean inDoubleQuote = false;
        boolean escaped = false;
        for (int index = 0; index < line.length(); index++) {
            char character = line.charAt(index);
            if (inDoubleQuote) {
                if (escaped) {
                    escaped = false;
                    continue;
                }
                if (character == '\\') {
                    escaped = true;
                    continue;
                }
                if (character == '"') {
                    inDoubleQuote = false;
                }
                continue;
            }
            if (inSingleQuote) {
                if (character != '\'') {
                    continue;
                }
                if (index + 1 < line.length() && line.charAt(index + 1) == '\'') {
                    index++;
                    continue;
                }
                inSingleQuote = false;
                continue;
            }
            if (character == '"') {
                inDoubleQuote = true;
                continue;
            }
            if (character == '\'') {
                inSingleQuote = true;
                continue;
            }
            if (character == '#' && (index == 0 || Character.isWhitespace(line.charAt(index - 1)))) {
                return index;
            }
        }
        return -1;
    }

    private static String readUtf8(InputStream input) throws IOException {
        Scanner scanner = new Scanner(input, StandardCharsets.UTF_8.name()).useDelimiter("\\A");
        return scanner.hasNext() ? scanner.next() : "";
    }

    /**
     * 尝试加载配置
     */
    public static boolean tryLoad(String configPath, DriverConfig[] configHolder) {
        DriverConfig config = load(configPath);
        if (config != null) {
            configHolder[0] = config;
            return true;
        }
        return false;
    }

    /**
     * 转换为HttpsOptions
     * 参照 IncubatorController 的 Spring Boot SSL 配置
     */
    public static HttpsOptions toHttpsOptions(DriverConfig config) {
        if (config == null || config.getServer() == null) {
            return new HttpsOptions();
        }

        DriverConfig.ServerConfig server = config.getServer();
        HttpsOptions options = new HttpsOptions();
        options.setPort(server.getPort());
        options.setHost(server.getHost());
        options.setUseHttps(server.isUseHttps());
        options.setMaxConcurrentRequests(server.getMaxConcurrentRequests());
        options.setMaxRequestBodyBytes(server.getMaxRequestBodyBytes());
        options.setFunctionQueueCapacity(server.getFunctionQueueCapacity());
        options.setIdempotencyCapacity(server.getIdempotencyCapacity());
        options.setShutdownTimeoutMs(server.getShutdownTimeoutMs());

        // 密钥库配置（对应 Spring Boot: server.ssl.key-store-*）
        if (server.getCertificate() != null) {
            options.setServerCertificatePath(server.getCertificate().getPath());
            options.setServerCertificatePassword(server.getCertificate().getPassword());
            if (server.getCertificate().getType() != null) {
                options.setKeyStoreType(server.getCertificate().getType());
            }
            options.setKeyAlias(server.getCertificate().getAlias());
        }

        // 信任库配置（对应 Spring Boot: server.ssl.trust-store-*）
        if (server.getTrustStore() != null) {
            options.setTrustStorePath(server.getTrustStore().getPath());
            options.setTrustStorePassword(server.getTrustStore().getPassword());
            if (server.getTrustStore().getType() != null) {
                options.setTrustStoreType(server.getTrustStore().getType());
            }
        }

        // SSL/TLS 配置（对应 Spring Boot: server.ssl.protocol, ciphers, enabled-protocols）
        if (server.getSsl() != null) {
            DriverConfig.SslConfig sslConfig = server.getSsl();
            if (sslConfig.getProtocol() != null) {
                options.setProtocol(sslConfig.getProtocol());
            }
            if (sslConfig.getEnabledProtocols() != null && !sslConfig.getEnabledProtocols().isEmpty()) {
                options.setEnabledProtocols(sslConfig.getEnabledProtocols().toArray(new String[0]));
            }
            if (sslConfig.getCiphers() != null && !sslConfig.getCiphers().isEmpty()) {
                options.setCiphers(sslConfig.getCiphers().toArray(new String[0]));
            }
        }

        // 客户端认证配置（对应 Spring Boot: server.ssl.client-auth）
        if (server.getClientAuth() != null) {
            if (!server.getClientAuth().isEnabled()) {
                options.setClientAuth(HttpsOptions.ClientAuthMode.NONE);
                options.setRequireClientCertificate(false);
            } else {
            String mode = server.getClientAuth().getMode();
            if (mode != null) {
                switch (mode.toLowerCase()) {
                    case "need":
                        options.setClientAuth(HttpsOptions.ClientAuthMode.NEED);
                        break;
                    case "want":
                        options.setClientAuth(HttpsOptions.ClientAuthMode.WANT);
                        break;
                    case "none":
                        options.setClientAuth(HttpsOptions.ClientAuthMode.NONE);
                        break;
                    default:
                        options.setClientAuth(HttpsOptions.ClientAuthMode.NEED);
                }
            }
            }
            if (server.getClientAuth().getTrustedThumbprints() != null) {
                options.setTrustedClientThumbprints(
                        server.getClientAuth().getTrustedThumbprints().toArray(new String[0])
                );
            }
        }

        // 回调配置
        if (config.getCallback() != null) {
            options.setCallbackUrl(config.getCallback().getUrl());
            options.setCallbackTimeoutMs(config.getCallback().getTimeoutMs());
            options.setEnableCallback(config.getCallback().isEnabled());
        }

        return options;
    }

    /**
     * 转换为DeviceConfiguration
     */
    public static DeviceConfiguration toDeviceConfiguration(DriverConfig config) {
        if (config == null || config.getDevice() == null) {
            return null;
        }

        DriverConfig.DeviceConfigSection device = config.getDevice();
        String commType = device.getCommunicationType();

        if (commType == null) {
            commType = "TCP";
        }

        DeviceConfiguration deviceConfig;

        switch (commType.toUpperCase()) {
            case "TCP":
                DriverConfig.TcpConfig tcp = device.getTcp();
                if (tcp != null) {
                    int connectTimeout = tcp.getConnectTimeoutMs() != null
                            ? tcp.getConnectTimeoutMs() : tcp.getTimeoutMs();
                    deviceConfig = DeviceConfiguration.createTcp(
                            device.getDeviceId(),
                            tcp.getHost(),
                            tcp.getPort(),
                            connectTimeout
                    );
                    deviceConfig.setReadTimeout(tcp.getReadTimeoutMs() != null
                            ? tcp.getReadTimeoutMs() : tcp.getTimeoutMs());
                    deviceConfig.setWriteTimeout(tcp.getWriteTimeoutMs() != null
                            ? tcp.getWriteTimeoutMs() : tcp.getTimeoutMs());
                } else {
                    deviceConfig = new DeviceConfiguration(device.getDeviceId(), CommunicationType.TCP);
                }
                break;

            case "HTTP":
                DriverConfig.HttpConfig http = device.getHttp();
                if (http != null) {
                    deviceConfig = DeviceConfiguration.createHttp(
                            device.getDeviceId(),
                            http.getBaseUrl(),
                            http.getTimeoutMs()
                    );
                } else {
                    deviceConfig = new DeviceConfiguration(device.getDeviceId(), CommunicationType.HTTP);
                }
                break;

            case "SERIAL":
                DriverConfig.SerialConfig serial = device.getSerial();
                if (serial != null) {
                    deviceConfig = DeviceConfiguration.createSerial(
                            device.getDeviceId(),
                            serial.getPort(),
                            serial.getBaudRate()
                    );
                    deviceConfig.setDataBits(serial.getDataBits());
                    deviceConfig.setStopBits(serial.getStopBits());
                    deviceConfig.setParity(serial.getParity());
                    deviceConfig.setEncoding(serial.getEncoding());
                    deviceConfig.setReadTimeout(serial.getReadTimeoutMs() != null
                            ? serial.getReadTimeoutMs() : serial.getTimeoutMs());
                    deviceConfig.setWriteTimeout(serial.getWriteTimeoutMs() != null
                            ? serial.getWriteTimeoutMs() : serial.getTimeoutMs());
                    deviceConfig.setFlowControl(serial.getFlowControl());
                    deviceConfig.setDtrEnable(serial.isDtrEnable());
                    deviceConfig.setRtsEnable(serial.isRtsEnable());
                    deviceConfig.setDiscardInputBeforeWrite(serial.isDiscardInputBeforeWrite());
                } else {
                    deviceConfig = new DeviceConfiguration(device.getDeviceId(), CommunicationType.SERIAL);
                }
                break;

            case "DLL":
                deviceConfig = DeviceConfiguration.createDll(device.getDeviceId());
                break;

            default:
                throw new IllegalArgumentException("Unsupported communication type: " + commType);
        }

        deviceConfig.setDeviceCallResources(device.getDeviceCallResources());
        deviceConfig.setDeviceCallTimeout(device.getDeviceCallTimeoutMs());
        if (device.getConnections() != null) {
            for (Map.Entry<String, com.ciai.controller.sdk.core.ConnectionConfiguration> entry
                    : device.getConnections().entrySet()) {
                entry.getValue().setName(entry.getKey());
            }
            deviceConfig.setConnections(new LinkedHashMap<>(device.getConnections()));
        }
        deviceConfig.setExtraSettings(new LinkedHashMap<>(device.getSettings()));

        return deviceConfig;
    }
}

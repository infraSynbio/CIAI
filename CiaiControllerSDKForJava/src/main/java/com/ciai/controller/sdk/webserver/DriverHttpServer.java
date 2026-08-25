package com.ciai.controller.sdk.webserver;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.ciai.controller.sdk.callback.HttpCallback;
import com.ciai.controller.sdk.config.DriverConfig;
import com.ciai.controller.sdk.config.YamlConfigLoader;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.logging.LoggerProvider;
import com.ciai.controller.sdk.model.*;
import org.slf4j.Logger;

import java.io.*;
import java.net.InetSocketAddress;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.SecureRandom;
import java.util.*;
import java.util.concurrent.*;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;
import com.sun.net.httpserver.HttpsConfigurator;
import com.sun.net.httpserver.HttpsParameters;
import com.sun.net.httpserver.HttpsServer;

import javax.net.ssl.*;

/**
 * HTTP/HTTPS服务器实现
 */
public class DriverHttpServer implements AutoCloseable {

    private static final Logger logger = LoggerProvider.createLogger(DriverHttpServer.class);
    private static final ObjectMapper objectMapper = new ObjectMapper()
            .setDefaultPropertyInclusion(JsonInclude.Include.ALWAYS)
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    private final HttpsOptions options;
    private final DeviceDriverBase driver;
    private final HttpCallback callback;
    private com.sun.net.httpserver.HttpServer server;
    private final ThreadPoolExecutor requestExecutor;
    private final ThreadPoolExecutor functionExecutor;
    private final Set<String> acceptedInstructions = ConcurrentHashMap.newKeySet();
    private final ConcurrentLinkedQueue<String> instructionOrder = new ConcurrentLinkedQueue<>();
    private volatile boolean running = false;

    public DriverHttpServer(HttpsOptions options, DeviceDriverBase driver) {
        this.options = options;
        this.driver = driver;
        this.callback = new HttpCallback(
                options.getCallbackUrl(),
                options.getCallbackTimeoutMs(),
                options.isEnableCallback()
        );
        this.requestExecutor = new ThreadPoolExecutor(
                options.getMaxConcurrentRequests(), options.getMaxConcurrentRequests(),
                0L, TimeUnit.MILLISECONDS,
                new ArrayBlockingQueue<>(options.getMaxConcurrentRequests() * 2),
                new ThreadPoolExecutor.CallerRunsPolicy());
        int functionWorkers = driver.getDriverAttribute().functionalResources();
        this.functionExecutor = new ThreadPoolExecutor(
                functionWorkers, functionWorkers, 0L, TimeUnit.MILLISECONDS,
                new ArrayBlockingQueue<>(options.getFunctionQueueCapacity()),
                new ThreadPoolExecutor.AbortPolicy());
    }

    /**
     * 启动服务器
     */
    public void start() throws IOException {
        if (running) {
            return;
        }

        options.validate();

        try {
            if (options.isUseHttps()) {
                startHttpsServer();
            } else {
                startHttpServer();
            }

            // 注册路由
            registerRoutes();

            server.setExecutor(requestExecutor);
            server.start();
            running = true;

            logger.info("Server started on {}://{}:{}",
                    options.isUseHttps() ? "https" : "http",
                    options.getHost(),
                    options.getPort());

        } catch (Exception e) {
            logger.error("Failed to start server", e);
            throw new IOException("Failed to start server: " + e.getMessage(), e);
        }
    }

    private void startHttpsServer() throws Exception {
        // 配置错误应在启动阶段直接报告，不能静默降级到用户未选择的协议。
        String protocol = options.getProtocol() != null ? options.getProtocol() : "TLSv1.2";
        SSLContext sslContext = SSLContext.getInstance(protocol);

        // 加载密钥库（服务端证书）
        KeyManagerFactory kmf = loadKeyStore();

        // 加载信任库（用于客户端证书验证）
        TrustManager[] trustManagers = createTrustManagers();

        // 初始化SSL上下文
        sslContext.init(kmf.getKeyManagers(), trustManagers, new SecureRandom());
        validateTlsConfiguration(sslContext);

        // 创建HTTPS服务器
        HttpsServer httpsServer = HttpsServer.create(
                new InetSocketAddress(options.getHost(), options.getPort()),
                0
        );

        // 配置HTTPS参数（参照 IncubatorController 的 TLSUtils 和 Spring Boot 配置）
        httpsServer.setHttpsConfigurator(new HttpsConfigurator(sslContext) {
            @Override
            public void configure(HttpsParameters params) {
                SSLContext context = getSSLContext();

                // 创建 SSLParameters 并配置
                SSLParameters sslParameters = context.getDefaultSSLParameters();

                // 设置启用的协议（参照 IncubatorController: server.ssl.enabled-protocols）
                if (options.getEnabledProtocols() != null && options.getEnabledProtocols().length > 0) {
                    sslParameters.setProtocols(options.getEnabledProtocols());
                }

                // 仅在用户显式配置时固定套件；默认使用JVM/操作系统安全策略。
                if (options.getCiphers() != null && options.getCiphers().length > 0) {
                    sslParameters.setCipherSuites(options.getCiphers());
                }

                // 设置客户端认证模式（参照 IncubatorController: server.ssl.client-auth）
                // 注意：必须设置在 sslParameters 上，而不是 params 上，
                // 因为 params.setSSLParameters() 会用 sslParameters 的值覆盖 params 的值
                switch (options.getClientAuth()) {
                    case NEED:
                        sslParameters.setNeedClientAuth(true);
                        break;
                    case WANT:
                        sslParameters.setWantClientAuth(true);
                        break;
                    case NONE:
                        sslParameters.setNeedClientAuth(false);
                        sslParameters.setWantClientAuth(false);
                        break;
                }

                // 应用 SSL 参数
                params.setSSLParameters(sslParameters);
            }
        });

        this.server = httpsServer;

        logger.info("HTTPS Server configured with protocol: {}, ciphers: {}, clientAuth: {}",
                protocol,
                options.getCiphers() != null ? String.join(",", options.getCiphers()) : "default",
                options.getClientAuth());
    }

    private void validateTlsConfiguration(SSLContext sslContext) {
        SSLParameters supported = sslContext.getSupportedSSLParameters();
        Set<String> supportedProtocols = new HashSet<>(Arrays.asList(supported.getProtocols()));
        if (options.getEnabledProtocols() != null) {
            for (String configured : options.getEnabledProtocols()) {
                if (!supportedProtocols.contains(configured)) {
                    throw new IllegalArgumentException("Unsupported TLS protocol '" + configured
                            + "'. JVM supports: " + supportedProtocols);
                }
            }
        }

        Set<String> supportedCiphers = new HashSet<>(Arrays.asList(supported.getCipherSuites()));
        if (options.getCiphers() != null) {
            for (String configured : options.getCiphers()) {
                if (!supportedCiphers.contains(configured)) {
                    throw new IllegalArgumentException("Unsupported TLS cipher '" + configured
                            + "' for this JVM");
                }
            }
        }
    }

    private void startHttpServer() throws IOException {
        server = HttpServer.create(
                new InetSocketAddress(options.getHost(), options.getPort()),
                0
        );
    }

    /**
     * 加载密钥库（服务端证书）
     * 参照 IncubatorController 的 TLSUtils.createSSLContext 实现
     */
    private KeyManagerFactory loadKeyStore() throws Exception {
        String certPath = options.getServerCertificatePath();
        String certPassword = options.getServerCertificatePassword();
        String keyStoreType = options.getKeyStoreType() != null ? options.getKeyStoreType() : "PKCS12";

        if (certPath == null || certPath.isEmpty()) {
            throw new IllegalStateException("Server certificate path (key-store) is required for HTTPS");
        }

        logger.info("Loading key store from: {} (type: {})", certPath, keyStoreType);

        // 加载 KeyStore（参照 IncubatorController: TLSUtils.createSSLContext）
        KeyStore keyStore = KeyStore.getInstance(keyStoreType);
        try (FileInputStream fis = new FileInputStream(certPath)) {
            char[] password = certPassword != null ? certPassword.toCharArray() : new char[0];
            keyStore.load(fis, password);
        }

        // 初始化 KeyManagerFactory（参照 IncubatorController: 使用 SunX509 算法）
        KeyManagerFactory kmf = KeyManagerFactory.getInstance("SunX509");
        kmf.init(keyStore, certPassword != null ? certPassword.toCharArray() : new char[0]);

        logger.info("Key store loaded successfully");
        return kmf;
    }

    /**
     * 创建信任管理器（用于客户端证书验证）
     * 参照 IncubatorController 的 TLSUtils.createSSLContext 实现
     */
    private TrustManager[] createTrustManagers() throws Exception {
        // 不启用客户端认证时交给JSSE默认信任策略；服务端不会请求客户端证书。
        if (options.getClientAuth() == HttpsOptions.ClientAuthMode.NONE) {
            logger.info("Client authentication disabled");
            return null;
        }

        // 加载信任库（参照 IncubatorController: TLSUtils.createSSLContext）
        String trustStorePath = options.getTrustStorePath();
        String trustStorePassword = options.getTrustStorePassword();
        String trustStoreType = options.getTrustStoreType() != null ? options.getTrustStoreType() : "PKCS12";

        KeyStore trustStore;

        // 如果配置了信任库路径，加载信任库
        if (trustStorePath != null && !trustStorePath.isEmpty()) {
            logger.info("Loading trust store from: {} (type: {})", trustStorePath, trustStoreType);
            trustStore = KeyStore.getInstance(trustStoreType);
            try (FileInputStream fis = new FileInputStream(trustStorePath)) {
                char[] password = trustStorePassword != null ? trustStorePassword.toCharArray() : new char[0];
                trustStore.load(fis, password);
            }
        } else {
            // 如果没有配置信任库，使用密钥库作为信任库（与 IncubatorController 一致）
            // IncubatorController: trust-store: classpath:server.controller (与 key-store 相同)
            logger.info("No separate trust store configured, using key store as trust store");
            String keyStorePath = options.getServerCertificatePath();
            String keyStorePassword = options.getServerCertificatePassword();
            String keyStoreType = options.getKeyStoreType() != null ? options.getKeyStoreType() : "PKCS12";

            trustStore = KeyStore.getInstance(keyStoreType);
            try (FileInputStream fis = new FileInputStream(keyStorePath)) {
                char[] password = keyStorePassword != null ? keyStorePassword.toCharArray() : new char[0];
                trustStore.load(fis, password);
            }
        }

        // 初始化 TrustManagerFactory（参照 IncubatorController: 使用 SunX509 算法）
        TrustManagerFactory tmf = TrustManagerFactory.getInstance("SunX509");
        tmf.init(trustStore);

        logger.info("Trust managers created successfully");
        return tmf.getTrustManagers();
    }

    private void registerRoutes() {
        // Info端点
        server.createContext(RouteBuilder.Endpoints.INFO, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.INFO)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("GET")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                Result<RegisterInfo> result = driver.getRegisterInfo();
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logger.error("Handle Info error", e);
                sendJsonResponse(exchange, 500, Result.serverError("Internal Server Error"));
            }
        });

        // HeartBeat端点
        server.createContext(RouteBuilder.Endpoints.HEART_BEAT, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.HEART_BEAT)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("GET")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                Result<HeartBeatInfo> result = driver.getHeartBeat();
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logger.error("Handle HeartBeat error", e);
                sendJsonResponse(exchange, 500, Result.serverError("Internal Server Error"));
            }
        });

        // Function端点
        server.createContext(RouteBuilder.Endpoints.FUNCTION, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.FUNCTION)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("POST")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                String body = readRequestBody(exchange);
                FunctionData data = objectMapper.readValue(body, FunctionData.class);
                if (data == null || data.getFunctionName() == null
                        || data.getFunctionName().trim().isEmpty()) {
                    sendJsonResponse(exchange, 400, Result.parametersMissing("functionName is required"));
                    return;
                }

                String instructionId = data.getInstructionId();
                String nestId = data.getNestId();
                boolean hasInstructionId = instructionId != null && !instructionId.trim().isEmpty();
                if (hasInstructionId && !acceptedInstructions.add(instructionId)) {
                    sendJsonResponse(exchange, 200, Result.success("Function already accepted"));
                    return;
                }

                try {
                    functionExecutor.execute(() -> {
                    try {
                        Result<Finish> result = driver.executeFunction(data);
                        if (result.isSuccess() && result.getData() != null) {
                            Finish finish = result.getData();
                            if (finish.getInstructionId() == null) finish.setInstructionId(instructionId);
                            if (finish.getNestId() == null) finish.setNestId(nestId);
                            callback.postFinishAsync(finish).join();
                        } else {
                            Finish finish = Finish.error(result.getMessage());
                            finish.setInstructionId(instructionId);
                            finish.setNestId(nestId);
                            callback.postFinishAsync(finish).join();
                        }
                    } catch (Exception e) {
                        logger.error("Execute function async error", e);
                        Finish finish = Finish.error(e.getMessage());
                        finish.setInstructionId(instructionId);
                        finish.setNestId(nestId);
                        callback.postFinishAsync(finish).join();
                    }
                    });
                } catch (RejectedExecutionException e) {
                    if (hasInstructionId) acceptedInstructions.remove(instructionId);
                    sendJsonResponse(exchange, 429, Result.failed("Function queue is full; retry later"));
                    return;
                }

                if (hasInstructionId) {
                    instructionOrder.offer(instructionId);
                    trimInstructionHistory();
                }

                sendJsonResponse(exchange, 200, Result.success("Function accepted"));
            } catch (Exception e) {
                logRequestError("Function", e);
                sendRequestError(exchange, e);
            }
        });

        // Operation端点
        server.createContext(RouteBuilder.Endpoints.OPERATION, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.OPERATION)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("POST")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                String body = readRequestBody(exchange);
                OperationData data = objectMapper.readValue(body, OperationData.class);
                Result<Boolean> result = driver.executeOperation(data);
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logRequestError("Operation", e);
                sendRequestError(exchange, e);
            }
        });

        // Set端点
        server.createContext(RouteBuilder.Endpoints.SET, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.SET)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("POST")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                String body = readRequestBody(exchange);
                List<SetData> dataList = objectMapper.readValue(body,
                        objectMapper.getTypeFactory().constructCollectionType(List.class, SetData.class));
                Result<Boolean> result = driver.executeSet(dataList);
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logRequestError("Set", e);
                sendRequestError(exchange, e);
            }
        });

        // Get端点
        server.createContext(RouteBuilder.Endpoints.GET, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.GET)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("GET")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                Result<List<GetReturn>> result = driver.getStatus();
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logger.error("Handle Get error", e);
                sendJsonResponse(exchange, 500, Result.serverError("Internal Server Error"));
            }
        });

        // EnterAndExit端点
        server.createContext(RouteBuilder.Endpoints.ENTER_AND_EXIT, exchange -> {
            if (!isExactPath(exchange, RouteBuilder.Endpoints.ENTER_AND_EXIT)) {
                sendJsonResponse(exchange, 404, Result.failed("Not Found"));
                return;
            }
            if (!exchange.getRequestMethod().equals("POST")) {
                sendJsonResponse(exchange, 405, Result.failed("Method Not Allowed"));
                return;
            }
            try {
                String body = readRequestBody(exchange);
                EnterOrExitData data = objectMapper.readValue(body, EnterOrExitData.class);
                Result<Finish> result = driver.executeEnterExit(data);
                sendJsonResponse(exchange, 200, result);
            } catch (Exception e) {
                logRequestError("EnterAndExit", e);
                sendRequestError(exchange, e);
            }
        });
        server.createContext("/", exchange ->
                sendJsonResponse(exchange, 404, Result.failed("Not Found")));
    }

    private boolean isExactPath(HttpExchange exchange, String expected) {
        String path = exchange.getRequestURI().getPath();
        if (path != null && path.length() > 1 && path.endsWith("/")) {
            path = path.substring(0, path.length() - 1);
        }
        return expected.equals(path);
    }

    private String readRequestBody(HttpExchange exchange) throws IOException {
        String contentLength = exchange.getRequestHeaders().getFirst("Content-Length");
        if (contentLength != null) {
            try {
                if (Long.parseLong(contentLength) > options.getMaxRequestBodyBytes()) {
                    throw new RequestBodyTooLargeException();
                }
            } catch (NumberFormatException e) {
                throw new IOException("Invalid Content-Length", e);
            }
        }
        try (InputStream input = exchange.getRequestBody();
             ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = input.read(buffer)) >= 0) {
                if (output.size() + count > options.getMaxRequestBodyBytes()) {
                    throw new RequestBodyTooLargeException();
                }
                output.write(buffer, 0, count);
            }
            return new String(output.toByteArray(), StandardCharsets.UTF_8);
        }
    }

    private void sendRequestError(HttpExchange exchange, Exception error) throws IOException {
        if (error instanceof RequestBodyTooLargeException) {
            sendJsonResponse(exchange, 413, Result.failed("Request body is too large"));
        } else {
            sendJsonResponse(exchange, 400, Result.failed("Invalid request"));
        }
    }

    private void logRequestError(String endpoint, Exception error) {
        if (error instanceof RequestBodyTooLargeException) {
            logger.warn("{} request body exceeded {} bytes",
                    endpoint, options.getMaxRequestBodyBytes());
        } else {
            logger.warn("Invalid {} request: {}", endpoint, error.getMessage());
        }
    }

    private void trimInstructionHistory() {
        while (acceptedInstructions.size() > options.getIdempotencyCapacity()) {
            String oldest = instructionOrder.poll();
            if (oldest == null) break;
            acceptedInstructions.remove(oldest);
        }
    }

    private void sendJsonResponse(HttpExchange exchange, int statusCode, Object response) throws IOException {
        String json = objectMapper.writeValueAsString(response);
        byte[] bytes = json.getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=UTF-8");
        exchange.sendResponseHeaders(statusCode, bytes.length);
        try (OutputStream os = exchange.getResponseBody()) {
            os.write(bytes);
        }
    }

    /**
     * 停止服务器
     */
    public void stop() {
        if (!running) {
            return;
        }

        running = false;
        if (server != null) {
            server.stop(Math.max(1, options.getShutdownTimeoutMs() / 1000));
        }
        requestExecutor.shutdown();
        functionExecutor.shutdown();
        awaitTermination(requestExecutor, "request");
        awaitTermination(functionExecutor, "function");
        callback.close();
        logger.info("Server stopped");
    }

    private void awaitTermination(ExecutorService executorService, String name) {
        try {
            if (!executorService.awaitTermination(options.getShutdownTimeoutMs(), TimeUnit.MILLISECONDS)) {
                logger.warn("{} executor did not stop within {}ms", name, options.getShutdownTimeoutMs());
                executorService.shutdownNow();
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            executorService.shutdownNow();
        }
    }

    private static final class RequestBodyTooLargeException extends IOException {
    }

    public boolean isRunning() {
        return running;
    }

    @Override
    public void close() {
        stop();
        driver.close();
    }
}

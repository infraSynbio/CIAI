package com.ciai.controller.sdk.communication;

import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.logging.LoggerProvider;
import org.slf4j.Logger;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * HTTP通信实现
 */
public class HttpCommunication implements ICommunication, AutoCloseable {

    private static final Logger logger = LoggerProvider.createLogger(HttpCommunication.class);

    private final String baseUrl;
    private final int timeout;
    private final Map<String, String> defaultHeaders;
    private final ExecutorService executor = Executors.newFixedThreadPool(4);
    private volatile boolean connected = false;

    public HttpCommunication(String baseUrl) {
        this(baseUrl, 30000);
    }

    public HttpCommunication(String baseUrl, int timeout) {
        this(baseUrl, timeout, null);
    }

    public HttpCommunication(String baseUrl, int timeout, Map<String, String> defaultHeaders) {
        if (baseUrl == null || baseUrl.trim().isEmpty()) {
            throw new IllegalArgumentException("HTTP base URL is required");
        }
        if (timeout <= 0) {
            throw new IllegalArgumentException("HTTP timeout must be greater than zero");
        }
        this.baseUrl = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
        this.timeout = timeout;
        this.defaultHeaders = defaultHeaders == null ? Collections.<String,String>emptyMap()
                : new LinkedHashMap<>(defaultHeaders);
    }

    @Override
    public boolean isConnected() {
        return connected;
    }

    @Override
    public CompletableFuture<Boolean> connectAsync() {
        return CompletableFuture.completedFuture(connect());
    }

    @Override
    public CompletableFuture<Void> disconnectAsync() {
        connected = false;
        return CompletableFuture.completedFuture(null);
    }

    @Override
    public CompletableFuture<Boolean> sendAsync(byte[] data) {
        return CompletableFuture.supplyAsync(() -> send(data), executor);
    }

    @Override
    public CompletableFuture<byte[]> receiveAsync() {
        return CompletableFuture.supplyAsync(this::receive, executor);
    }

    @Override
    public CompletableFuture<byte[]> sendAndReceiveAsync(byte[] data) {
        return CompletableFuture.supplyAsync(() -> sendAndReceive(data), executor);
    }

    @Override
    public boolean connect() {
        connected = true;
        return true;
    }

    @Override
    public void disconnect() {
        connected = false;
    }

    @Override
    public boolean send(byte[] data) {
        return requestBytes("POST", "", data) != null;
    }

    @Override
    public byte[] receive() {
        return requestBytes("GET", "", null);
    }

    @Override
    public byte[] sendAndReceive(byte[] data) {
        return requestBytes("POST", "", data);
    }

    private byte[] requestBytes(String method, String endpoint, byte[] body) {
        if (!connected) {
            logger.error("HTTP client is disconnected");
            return null;
        }
        HttpURLConnection connection = null;
        try {
            String suffix = endpoint == null || endpoint.isEmpty()
                    ? "" : (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            connection = (HttpURLConnection) new URL(baseUrl + suffix).openConnection();
            connection.setRequestMethod(method);
            connection.setConnectTimeout(timeout);
            connection.setReadTimeout(timeout);
            applyDefaultHeaders(connection);
            if (body != null) {
                connection.setDoOutput(true);
                connection.setRequestProperty("Content-Type", "application/octet-stream");
                try (OutputStream output = connection.getOutputStream()) {
                    output.write(body);
                }
            }
            int status = connection.getResponseCode();
            if (status < 200 || status >= 300) {
                logger.error("HTTP {} failed: {}", method, status);
                return null;
            }
            try (InputStream input = connection.getInputStream();
                 ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[4096];
                int count;
                while ((count = input.read(buffer)) >= 0) {
                    output.write(buffer, 0, count);
                }
                return output.toByteArray();
            }
        } catch (Exception e) {
            logger.error("HTTP {} error", method, e);
            return null;
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    /** 任意HTTP方法、请求头和内容类型。 */
    public byte[] request(String method, String endpoint, byte[] body,
                          Map<String, String> headers, String contentType) {
        if (!connected) return null;
        HttpURLConnection connection = null;
        try {
            String suffix = endpoint == null || endpoint.isEmpty() ? "" :
                    (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            connection = (HttpURLConnection) new URL(baseUrl + suffix).openConnection();
            connection.setRequestMethod(method);
            connection.setConnectTimeout(timeout);
            connection.setReadTimeout(timeout);
            for (Map.Entry<String,String> h : defaultHeaders.entrySet()) connection.setRequestProperty(h.getKey(), h.getValue());
            if (headers != null) for (Map.Entry<String,String> h : headers.entrySet()) connection.setRequestProperty(h.getKey(), h.getValue());
            if (body != null) {
                connection.setDoOutput(true);
                connection.setRequestProperty("Content-Type", contentType == null ? "application/octet-stream" : contentType);
                try (OutputStream output = connection.getOutputStream()) { output.write(body); }
            }
            int status = connection.getResponseCode();
            if (status < 200 || status >= 300) return null;
            try (InputStream input = connection.getInputStream(); ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[4096]; int count;
                while ((count = input.read(buffer)) >= 0) output.write(buffer, 0, count);
                return output.toByteArray();
            }
        } catch (Exception e) { logger.error("HTTP request error", e); return null; }
        finally { if (connection != null) connection.disconnect(); }
    }

    public CompletableFuture<byte[]> requestAsync(final String method, final String endpoint,
            final byte[] body, final Map<String,String> headers, final String contentType) {
        return CompletableFuture.supplyAsync(() -> request(method, endpoint, body, headers, contentType), executor);
    }

    /**
     * GET请求
     */
    public CompletableFuture<String> getAsync(String endpoint) {
        return CompletableFuture.supplyAsync(() -> get(endpoint), executor);
    }

    /**
     * 同步GET请求
     */
    public String get(String endpoint) {
        try {
            String urlStr = baseUrl + (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            URL url = new URL(urlStr);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("GET");
            conn.setConnectTimeout(timeout);
            conn.setReadTimeout(timeout);
            applyDefaultHeaders(conn);

            int responseCode = conn.getResponseCode();
            logger.debug("HTTP GET {} -> {}", urlStr, responseCode);

            if (responseCode >= 200 && responseCode < 300) {
                return readResponse(conn);
            } else {
                logger.error("HTTP GET failed: {} - {}", responseCode, conn.getResponseMessage());
                return null;
            }
        } catch (Exception e) {
            logger.error("HTTP GET error", e);
            return null;
        }
    }

    /**
     * POST请求
     */
    public CompletableFuture<String> postAsync(String endpoint, String jsonContent) {
        return CompletableFuture.supplyAsync(() -> post(endpoint, jsonContent), executor);
    }

    /**
     * 同步POST请求
     */
    public String post(String endpoint, String jsonContent) {
        try {
            String urlStr = baseUrl + (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            URL url = new URL(urlStr);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(timeout);
            conn.setReadTimeout(timeout);
            applyDefaultHeaders(conn);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json");

            // 发送请求体
            if (jsonContent != null) {
                try (OutputStream os = conn.getOutputStream()) {
                    byte[] input = jsonContent.getBytes("UTF-8");
                    os.write(input, 0, input.length);
                }
            }

            int responseCode = conn.getResponseCode();
            logger.debug("HTTP POST {} -> {}", urlStr, responseCode);

            if (responseCode >= 200 && responseCode < 300) {
                return readResponse(conn);
            } else {
                logger.error("HTTP POST failed: {} - {}", responseCode, conn.getResponseMessage());
                return null;
            }
        } catch (Exception e) {
            logger.error("HTTP POST error", e);
            return null;
        }
    }

    /**
     * PUT请求
     */
    public CompletableFuture<String> putAsync(String endpoint, String jsonContent) {
        return CompletableFuture.supplyAsync(() -> put(endpoint, jsonContent), executor);
    }

    /**
     * 同步PUT请求
     */
    public String put(String endpoint, String jsonContent) {
        try {
            String urlStr = baseUrl + (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            URL url = new URL(urlStr);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("PUT");
            conn.setConnectTimeout(timeout);
            conn.setReadTimeout(timeout);
            applyDefaultHeaders(conn);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json");

            if (jsonContent != null) {
                try (OutputStream os = conn.getOutputStream()) {
                    byte[] input = jsonContent.getBytes("UTF-8");
                    os.write(input, 0, input.length);
                }
            }

            int responseCode = conn.getResponseCode();
            logger.debug("HTTP PUT {} -> {}", urlStr, responseCode);

            if (responseCode >= 200 && responseCode < 300) {
                return readResponse(conn);
            } else {
                logger.error("HTTP PUT failed: {} - {}", responseCode, conn.getResponseMessage());
                return null;
            }
        } catch (Exception e) {
            logger.error("HTTP PUT error", e);
            return null;
        }
    }

    /**
     * DELETE请求
     */
    public CompletableFuture<String> deleteAsync(String endpoint) {
        return CompletableFuture.supplyAsync(() -> delete(endpoint), executor);
    }

    /**
     * 同步DELETE请求
     */
    public String delete(String endpoint) {
        try {
            String urlStr = baseUrl + (endpoint.startsWith("/") ? endpoint : "/" + endpoint);
            URL url = new URL(urlStr);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("DELETE");
            conn.setConnectTimeout(timeout);
            conn.setReadTimeout(timeout);
            applyDefaultHeaders(conn);

            int responseCode = conn.getResponseCode();
            logger.debug("HTTP DELETE {} -> {}", urlStr, responseCode);

            if (responseCode >= 200 && responseCode < 300) {
                return readResponse(conn);
            } else {
                logger.error("HTTP DELETE failed: {} - {}", responseCode, conn.getResponseMessage());
                return null;
            }
        } catch (Exception e) {
            logger.error("HTTP DELETE error", e);
            return null;
        }
    }

    private String readResponse(HttpURLConnection conn) throws Exception {
        try (BufferedReader br = new BufferedReader(
                new InputStreamReader(conn.getInputStream(), "UTF-8"))) {
            StringBuilder response = new StringBuilder();
            String responseLine;
            while ((responseLine = br.readLine()) != null) {
                response.append(responseLine.trim());
            }
            return response.toString();
        }
    }

    private void applyDefaultHeaders(HttpURLConnection connection) {
        for (Map.Entry<String, String> header : defaultHeaders.entrySet())
            connection.setRequestProperty(header.getKey(), header.getValue());
    }

    public String getBaseUrl() {
        return baseUrl;
    }

    public int getTimeout() {
        return timeout;
    }

    @Override
    public void close() {
        connected = false;
        executor.shutdown();
    }
}

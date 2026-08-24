package com.ciai.controller.sdk.callback;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.ciai.controller.sdk.model.Finish;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * HTTP回调工具类
 */
public class HttpCallback implements AutoCloseable {

    private static final Logger logger = LoggerFactory.getLogger(HttpCallback.class);
    private static final ObjectMapper objectMapper = new ObjectMapper()
            .setDefaultPropertyInclusion(JsonInclude.Include.ALWAYS)
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    private final String callbackUrl;
    private final int timeoutMs;
    private final boolean enabled;
    private final ExecutorService executor = Executors.newFixedThreadPool(2);

    public HttpCallback(String callbackUrl) {
        this(callbackUrl, 30000, true);
    }

    public HttpCallback(String callbackUrl, int timeoutMs) {
        this(callbackUrl, timeoutMs, true);
    }

    public HttpCallback(String callbackUrl, int timeoutMs, boolean enabled) {
        this.callbackUrl = callbackUrl;
        this.timeoutMs = timeoutMs;
        this.enabled = enabled && callbackUrl != null && !callbackUrl.isEmpty();
    }

    /**
     * 是否启用
     */
    public boolean isEnabled() {
        return enabled;
    }

    /**
     * 异步发送完成回调
     */
    public CompletableFuture<Boolean> postFinishAsync(Finish finish) {
        if (!enabled) {
            logger.debug("Callback is disabled, skipping");
            return CompletableFuture.completedFuture(Boolean.TRUE);
        }

        return CompletableFuture.supplyAsync(() -> {
            try {
                String json = objectMapper.writeValueAsString(finish);
                return Boolean.valueOf(postRaw(json));
            } catch (Exception e) {
                logger.error("Post finish callback error", e);
                return Boolean.FALSE;
            }
        }, executor);
    }

    /**
     * 异步发送数据
     */
    public <T> CompletableFuture<Boolean> postAsync(T data) {
        if (!enabled) {
            return CompletableFuture.completedFuture(Boolean.TRUE);
        }

        return CompletableFuture.supplyAsync(() -> {
            try {
                String json = objectMapper.writeValueAsString(data);
                return Boolean.valueOf(postRaw(json));
            } catch (Exception e) {
                logger.error("Post callback error", e);
                return Boolean.FALSE;
            }
        }, executor);
    }

    /**
     * 异步发送原始JSON
     */
    public CompletableFuture<Boolean> postRawAsync(String jsonContent) {
        if (!enabled) {
            return CompletableFuture.completedFuture(Boolean.TRUE);
        }

        return CompletableFuture.supplyAsync(() -> Boolean.valueOf(postRaw(jsonContent)), executor);
    }

    /**
     * 同步发送原始JSON
     */
    public boolean postRaw(String jsonContent) {
        if (!enabled) {
            return true;
        }

        try {
            URL url = new URL(callbackUrl);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(timeoutMs);
            conn.setReadTimeout(timeoutMs);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json");

            try (OutputStream os = conn.getOutputStream()) {
                byte[] input = jsonContent.getBytes("UTF-8");
                os.write(input, 0, input.length);
            }

            int responseCode = conn.getResponseCode();

            if (responseCode >= 200 && responseCode < 300) {
                logger.debug("Callback successful: {}", responseCode);
                return true;
            } else {
                logger.error("Callback failed: {}", responseCode);
                return false;
            }
        } catch (Exception e) {
            logger.error("Callback error to: {}", callbackUrl, e);
            return false;
        }
    }

    /**
     * 创建禁用的回调
     */
    public static HttpCallback createDisabled() {
        return new HttpCallback(null, 0, false);
    }

    @Override
    public void close() {
        executor.shutdown();
        try {
            if (!executor.awaitTermination(Math.max(1000, timeoutMs), java.util.concurrent.TimeUnit.MILLISECONDS)) {
                executor.shutdownNow();
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            executor.shutdownNow();
        }
    }
}

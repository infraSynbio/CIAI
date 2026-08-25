package com.ciai.controller.sdk.webserver;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.ciai.controller.sdk.callback.HttpCallback;
import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.model.*;
import com.ciai.controller.sdk.logging.LoggerProvider;
import org.slf4j.Logger;

import java.util.List;
import java.util.concurrent.CompletableFuture;

/**
 * HTTP响应
 */
public class HttpResponse {

    private static final ObjectMapper objectMapper = new ObjectMapper()
            .setDefaultPropertyInclusion(JsonInclude.Include.ALWAYS)
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    private int statusCode;
    private String contentType = "application/json";
    private String body;

    public HttpResponse() {
    }

    public HttpResponse(int statusCode, String body) {
        this.statusCode = statusCode;
        this.body = body;
    }

    public HttpResponse(int statusCode, String contentType, String body) {
        this.statusCode = statusCode;
        this.contentType = contentType;
        this.body = body;
    }

    // Getters and Setters
    public int getStatusCode() {
        return statusCode;
    }

    public void setStatusCode(int statusCode) {
        this.statusCode = statusCode;
    }

    public String getContentType() {
        return contentType;
    }

    public void setContentType(String contentType) {
        this.contentType = contentType;
    }

    public String getBody() {
        return body;
    }

    public void setBody(String body) {
        this.body = body;
    }

    // Static factory methods
    public static HttpResponse ok(Object data) {
        try {
            String json = objectMapper.writeValueAsString(data);
            return new HttpResponse(200, json);
        } catch (Exception e) {
            return internalError("Serialization error: " + e.getMessage());
        }
    }

    public static HttpResponse ok(String body) {
        return new HttpResponse(200, body);
    }

    public static HttpResponse notFound(String message) {
        return new HttpResponse(404, message);
    }

    public static HttpResponse badRequest(String message) {
        return new HttpResponse(400, message);
    }

    public static HttpResponse internalError(String message) {
        return new HttpResponse(500, message);
    }

    public static HttpResponse forbidden(String message) {
        return new HttpResponse(403, message);
    }
}

/**
 * 请求处理器
 */
class RequestHandler {

    private static final Logger logger = LoggerProvider.createLogger(RequestHandler.class);
    private static final ObjectMapper objectMapper = new ObjectMapper()
            .setDefaultPropertyInclusion(JsonInclude.Include.ALWAYS)
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);

    /**
     * 处理Function请求
     */
    public static HttpResponse handleFunction(DeviceDriverBase driver, String body) {
        try {
            FunctionData data = objectMapper.readValue(body, FunctionData.class);
            if (data == null || data.getFunctionName() == null) {
                return HttpResponse.badRequest("Invalid function data");
            }

            // 异步执行并返回
            CompletableFuture.runAsync(() -> {
                try {
                    Result<Finish> result = driver.executeFunction(data);

                    // 回调
                    if (result.isSuccess() && result.getData() != null) {
                        Finish finish = result.getData();
                        finish.setInstructionId(data.getInstructionId());
                        finish.setNestId(data.getNestId());
                        // 这里可以添加回调逻辑
                    }
                } catch (Exception e) {
                    logger.error("Execute function async error", e);
                }
            });

            // 立即返回接受响应
            return HttpResponse.ok(Result.success("Function accepted"));
        } catch (Exception e) {
            logger.error("Handle function error", e);
            return HttpResponse.badRequest("Invalid request body: " + e.getMessage());
        }
    }

    /**
     * 处理Operation请求
     */
    public static HttpResponse handleOperation(DeviceDriverBase driver, String body) {
        try {
            OperationData data = objectMapper.readValue(body, OperationData.class);
            if (data == null || data.getOperationName() == null) {
                return HttpResponse.badRequest("Invalid operation data");
            }

            Result<Boolean> result = driver.executeOperation(data);
            return HttpResponse.ok(result);
        } catch (Exception e) {
            logger.error("Handle operation error", e);
            return HttpResponse.badRequest("Invalid request body: " + e.getMessage());
        }
    }

    /**
     * 处理Set请求
     */
    public static HttpResponse handleSet(DeviceDriverBase driver, String body) {
        try {
            List<SetData> dataList = objectMapper.readValue(body, new TypeReference<List<SetData>>() {});
            if (dataList == null || dataList.isEmpty()) {
                return HttpResponse.badRequest("Invalid set data");
            }

            Result<Boolean> result = driver.executeSet(dataList);
            return HttpResponse.ok(result);
        } catch (Exception e) {
            logger.error("Handle set error", e);
            return HttpResponse.badRequest("Invalid request body: " + e.getMessage());
        }
    }

    /**
     * 处理EnterAndExit请求
     */
    public static HttpResponse handleEnterAndExit(DeviceDriverBase driver, String body) {
        try {
            EnterOrExitData data = objectMapper.readValue(body, EnterOrExitData.class);
            if (data == null || data.getEnterOrExitName() == null) {
                return HttpResponse.badRequest("Invalid enter/exit data");
            }

            Result<Finish> result = driver.executeEnterExit(data);
            return HttpResponse.ok(result);
        } catch (Exception e) {
            logger.error("Handle enter/exit error", e);
            return HttpResponse.badRequest("Invalid request body: " + e.getMessage());
        }
    }
}

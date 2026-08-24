package com.ciai.controller.sdk.model;

/**
 * 通用响应码
 */
public final class CommonCode {
    public static final String SUCCESS = "message.common.success";
    public static final String FAILED = "message.common.failed";
    public static final String UNAUTHORIZED = "message.common.unauthorized";
    public static final String TIMEOUT = "message.common.timeout";
    public static final String SERVER_ERROR = "message.common.server.error";
    public static final String PARAMETERS_MISSING = "message.common.parameters.missing";

    private CommonCode() {
        // 私有构造函数，防止实例化
    }
}

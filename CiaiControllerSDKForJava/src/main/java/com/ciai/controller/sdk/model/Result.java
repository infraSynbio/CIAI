package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 统一响应结果
 */
public class Result<T> {

    @JsonProperty("code")
    private String code;

    @JsonProperty("message")
    private String message;

    @JsonProperty("data")
    private T data;

    public Result() {
    }

    public Result(String code, String message) {
        this.code = code;
        this.message = message;
    }

    public Result(String code, String message, T data) {
        this.code = code;
        this.message = message;
        this.data = data;
    }

    @JsonIgnore
    public boolean isSuccess() {
        return CommonCode.SUCCESS.equals(code);
    }

    // Getters and Setters
    public String getCode() {
        return code;
    }

    public void setCode(String code) {
        this.code = code;
    }

    public String getMessage() {
        return message;
    }

    public void setMessage(String message) {
        this.message = message;
    }

    public T getData() {
        return data;
    }

    public void setData(T data) {
        this.data = data;
    }

    // Static factory methods
    public static <T> Result<T> success() {
        return new Result<>(CommonCode.SUCCESS, "Success");
    }

    public static <T> Result<T> success(T data) {
        return new Result<>(CommonCode.SUCCESS, "Success", data);
    }

    public static <T> Result<T> success(String message, T data) {
        return new Result<>(CommonCode.SUCCESS, message, data);
    }

    public static <T> Result<T> failed() {
        return new Result<>(CommonCode.FAILED, "Failed");
    }

    public static <T> Result<T> failed(String message) {
        return new Result<>(CommonCode.FAILED, message);
    }

    public static <T> Result<T> failed(String code, String message) {
        return new Result<>(code, message);
    }

    public static <T> Result<T> unauthorized() {
        return new Result<>(CommonCode.UNAUTHORIZED, "Unauthorized");
    }

    public static <T> Result<T> timeout() {
        return new Result<>(CommonCode.TIMEOUT, "Timeout");
    }

    public static <T> Result<T> serverError() {
        return new Result<>(CommonCode.SERVER_ERROR, "Server Error");
    }

    public static <T> Result<T> serverError(String message) {
        return new Result<>(CommonCode.SERVER_ERROR, message);
    }

    public static <T> Result<T> parametersMissing() {
        return new Result<>(CommonCode.PARAMETERS_MISSING, "Parameters Missing");
    }

    public static <T> Result<T> parametersMissing(String message) {
        return new Result<>(CommonCode.PARAMETERS_MISSING, message);
    }
}

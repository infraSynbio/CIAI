package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 操作接口数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class OperationData {

    @JsonProperty("operationName")
    private String operationName;

    @JsonProperty("operationParam")
    private Object operationParam;

    // Getters and Setters
    public String getOperationName() {
        return operationName;
    }

    public void setOperationName(String operationName) {
        this.operationName = operationName;
    }

    public Object getOperationParam() {
        return operationParam;
    }

    public void setOperationParam(Object operationParam) {
        this.operationParam = operationParam;
    }
}

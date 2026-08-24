package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 获取状态返回数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class GetReturn {

    @JsonProperty("getName")
    private String getName;

    @JsonProperty("getValue")
    private String getValue;

    public GetReturn() {
    }

    public GetReturn(String getName, String getValue) {
        this.getName = getName;
        this.getValue = getValue;
    }

    // Getters and Setters
    public String getGetName() {
        return getName;
    }

    public void setGetName(String getName) {
        this.getName = getName;
    }

    public String getGetValue() {
        return getValue;
    }

    public void setGetValue(String getValue) {
        this.getValue = getValue;
    }
}

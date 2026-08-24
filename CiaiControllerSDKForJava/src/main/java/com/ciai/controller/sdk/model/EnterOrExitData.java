package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 进出操作数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EnterOrExitData {

    @JsonProperty("enterOrExitName")
    private String enterOrExitName;

    @JsonProperty("enterOrExitValue")
    private Object enterOrExitValue;

    // Getters and Setters
    public String getEnterOrExitName() {
        return enterOrExitName;
    }

    public void setEnterOrExitName(String enterOrExitName) {
        this.enterOrExitName = enterOrExitName;
    }

    public Object getEnterOrExitValue() {
        return enterOrExitValue;
    }

    public void setEnterOrExitValue(Object enterOrExitValue) {
        this.enterOrExitValue = enterOrExitValue;
    }
}

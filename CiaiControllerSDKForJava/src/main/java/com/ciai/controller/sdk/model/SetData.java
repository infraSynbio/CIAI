package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设置数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class SetData {

    @JsonProperty("setName")
    private String setName;

    @JsonProperty("setValue")
    private String setValue;

    // Getters and Setters
    public String getSetName() {
        return setName;
    }

    public void setSetName(String setName) {
        this.setName = setName;
    }

    public String getSetValue() {
        return setValue;
    }

    public void setSetValue(String setValue) {
        this.setValue = setValue;
    }
}

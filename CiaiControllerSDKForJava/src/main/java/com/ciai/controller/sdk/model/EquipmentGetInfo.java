package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设备状态获取信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EquipmentGetInfo {

    @JsonProperty("getName")
    private String getName;

    @JsonProperty("getTitleCN")
    private String getTitleCN;

    @JsonProperty("getTitleEN")
    private String getTitleEN;

    @JsonProperty("getType")
    private String getType;

    @JsonProperty("getUnit")
    private String getUnit;

    @JsonProperty("getDescription")
    private String getDescription;

    // Getters and Setters
    public String getGetName() {
        return getName;
    }

    public void setGetName(String getName) {
        this.getName = getName;
    }

    public String getGetTitleCN() {
        return getTitleCN;
    }

    public void setGetTitleCN(String getTitleCN) {
        this.getTitleCN = getTitleCN;
    }

    public String getGetTitleEN() {
        return getTitleEN;
    }

    public void setGetTitleEN(String getTitleEN) {
        this.getTitleEN = getTitleEN;
    }

    public String getGetType() {
        return getType;
    }

    public void setGetType(String getType) {
        this.getType = getType;
    }

    public String getGetUnit() {
        return getUnit;
    }

    public void setGetUnit(String getUnit) {
        this.getUnit = getUnit;
    }

    public String getGetDescription() {
        return getDescription;
    }

    public void setGetDescription(String getDescription) {
        this.getDescription = getDescription;
    }
}

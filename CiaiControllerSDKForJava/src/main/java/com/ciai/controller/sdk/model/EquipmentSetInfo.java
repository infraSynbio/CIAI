package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.List;

/**
 * 设备参数设置信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EquipmentSetInfo {

    @JsonProperty("setName")
    private String setName;

    @JsonProperty("setTitleCN")
    private String setTitleCN;

    @JsonProperty("setTitleEN")
    private String setTitleEN;

    @JsonProperty("setType")
    private String setType;

    @JsonProperty("setValue")
    private List<String> setValue;

    @JsonProperty("setUnit")
    private String setUnit;

    @JsonProperty("setDescription")
    private String setDescription;

    // Getters and Setters
    public String getSetName() {
        return setName;
    }

    public void setSetName(String setName) {
        this.setName = setName;
    }

    public String getSetTitleCN() {
        return setTitleCN;
    }

    public void setSetTitleCN(String setTitleCN) {
        this.setTitleCN = setTitleCN;
    }

    public String getSetTitleEN() {
        return setTitleEN;
    }

    public void setSetTitleEN(String setTitleEN) {
        this.setTitleEN = setTitleEN;
    }

    public String getSetType() {
        return setType;
    }

    public void setSetType(String setType) {
        this.setType = setType;
    }

    public List<String> getSetValue() {
        return setValue;
    }

    public void setSetValue(List<String> setValue) {
        this.setValue = setValue;
    }

    public String getSetUnit() {
        return setUnit;
    }

    public void setSetUnit(String setUnit) {
        this.setUnit = setUnit;
    }

    public String getSetDescription() {
        return setDescription;
    }

    public void setSetDescription(String setDescription) {
        this.setDescription = setDescription;
    }
}

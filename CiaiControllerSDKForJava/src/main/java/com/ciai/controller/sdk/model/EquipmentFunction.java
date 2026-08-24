package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设备功能信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EquipmentFunction {

    @JsonProperty("functionName")
    private String functionName;

    @JsonProperty("functionTitleCN")
    private String functionTitleCN;

    @JsonProperty("functionTitleEN")
    private String functionTitleEN;

    @JsonProperty("functionDescription")
    private String functionDescription;

    @JsonProperty("functionDefaultPeriod")
    private String functionDefaultPeriod;

    @JsonProperty("functionCategoryCN")
    private String functionCategoryCN;

    @JsonProperty("functionCategoryEN")
    private String functionCategoryEN;

    @JsonProperty("iconBlack")
    private String iconBlack;

    @JsonProperty("iconWhite")
    private String iconWhite;

    @JsonProperty("functionFormJsonStructure")
    private String functionFormJsonStructure;

    // Getters and Setters
    public String getFunctionName() {
        return functionName;
    }

    public void setFunctionName(String functionName) {
        this.functionName = functionName;
    }

    public String getFunctionTitleCN() {
        return functionTitleCN;
    }

    public void setFunctionTitleCN(String functionTitleCN) {
        this.functionTitleCN = functionTitleCN;
    }

    public String getFunctionTitleEN() {
        return functionTitleEN;
    }

    public void setFunctionTitleEN(String functionTitleEN) {
        this.functionTitleEN = functionTitleEN;
    }

    public String getFunctionDescription() {
        return functionDescription;
    }

    public void setFunctionDescription(String functionDescription) {
        this.functionDescription = functionDescription;
    }

    public String getFunctionDefaultPeriod() {
        return functionDefaultPeriod;
    }

    public void setFunctionDefaultPeriod(String functionDefaultPeriod) {
        this.functionDefaultPeriod = functionDefaultPeriod;
    }

    public String getFunctionCategoryCN() {
        return functionCategoryCN;
    }

    public void setFunctionCategoryCN(String functionCategoryCN) {
        this.functionCategoryCN = functionCategoryCN;
    }

    public String getFunctionCategoryEN() {
        return functionCategoryEN;
    }

    public void setFunctionCategoryEN(String functionCategoryEN) {
        this.functionCategoryEN = functionCategoryEN;
    }

    public String getIconBlack() {
        return iconBlack;
    }

    public void setIconBlack(String iconBlack) {
        this.iconBlack = iconBlack;
    }

    public String getIconWhite() {
        return iconWhite;
    }

    public void setIconWhite(String iconWhite) {
        this.iconWhite = iconWhite;
    }

    public String getFunctionFormJsonStructure() {
        return functionFormJsonStructure;
    }

    public void setFunctionFormJsonStructure(String functionFormJsonStructure) {
        this.functionFormJsonStructure = functionFormJsonStructure;
    }
}

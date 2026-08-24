package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设备操作信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EquipmentOperation {

    @JsonProperty("operationName")
    private String operationName;

    @JsonProperty("operationTitleCN")
    private String operationTitleCN;

    @JsonProperty("operationTitleEN")
    private String operationTitleEN;

    @JsonProperty("operationDescription")
    private String operationDescription;

    @JsonProperty("operationFormJsonStructure")
    private String operationFormJsonStructure;

    // Getters and Setters
    public String getOperationName() {
        return operationName;
    }

    public void setOperationName(String operationName) {
        this.operationName = operationName;
    }

    public String getOperationTitleCN() {
        return operationTitleCN;
    }

    public void setOperationTitleCN(String operationTitleCN) {
        this.operationTitleCN = operationTitleCN;
    }

    public String getOperationTitleEN() {
        return operationTitleEN;
    }

    public void setOperationTitleEN(String operationTitleEN) {
        this.operationTitleEN = operationTitleEN;
    }

    public String getOperationDescription() {
        return operationDescription;
    }

    public void setOperationDescription(String operationDescription) {
        this.operationDescription = operationDescription;
    }

    public String getOperationFormJsonStructure() {
        return operationFormJsonStructure;
    }

    public void setOperationFormJsonStructure(String operationFormJsonStructure) {
        this.operationFormJsonStructure = operationFormJsonStructure;
    }
}

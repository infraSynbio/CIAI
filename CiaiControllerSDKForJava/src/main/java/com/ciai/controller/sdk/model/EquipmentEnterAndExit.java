package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设备进出信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class EquipmentEnterAndExit {

    @JsonProperty("enterAndExitName")
    private String enterAndExitName;

    @JsonProperty("enterAndExitTitleCN")
    private String enterAndExitTitleCN;

    @JsonProperty("enterAndExitTitleEN")
    private String enterAndExitTitleEN;

    // Getters and Setters
    public String getEnterAndExitName() {
        return enterAndExitName;
    }

    public void setEnterAndExitName(String enterAndExitName) {
        this.enterAndExitName = enterAndExitName;
    }

    public String getEnterAndExitTitleCN() {
        return enterAndExitTitleCN;
    }

    public void setEnterAndExitTitleCN(String enterAndExitTitleCN) {
        this.enterAndExitTitleCN = enterAndExitTitleCN;
    }

    public String getEnterAndExitTitleEN() {
        return enterAndExitTitleEN;
    }

    public void setEnterAndExitTitleEN(String enterAndExitTitleEN) {
        this.enterAndExitTitleEN = enterAndExitTitleEN;
    }
}

package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 功能接口数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class FunctionData {

    @JsonProperty("functionName")
    private String functionName;

    @JsonProperty("instructionId")
    private String instructionId;

    @JsonProperty("labwareInfo")
    private Labware labwareInfo;

    @JsonProperty("equipmentName")
    private String equipmentName;

    @JsonProperty("nestId")
    private String nestId;

    @JsonProperty("userId")
    private String userId;

    @JsonProperty("taskId")
    private String taskId;

    @JsonProperty("functionParam")
    private Object functionParam;

    // Getters and Setters
    public String getFunctionName() {
        return functionName;
    }

    public void setFunctionName(String functionName) {
        this.functionName = functionName;
    }

    public String getInstructionId() {
        return instructionId;
    }

    public void setInstructionId(String instructionId) {
        this.instructionId = instructionId;
    }

    public Labware getLabwareInfo() {
        return labwareInfo;
    }

    public void setLabwareInfo(Labware labwareInfo) {
        this.labwareInfo = labwareInfo;
    }

    public String getEquipmentName() {
        return equipmentName;
    }

    public void setEquipmentName(String equipmentName) {
        this.equipmentName = equipmentName;
    }

    public String getNestId() {
        return nestId;
    }

    public void setNestId(String nestId) {
        this.nestId = nestId;
    }

    public String getUserId() {
        return userId;
    }

    public void setUserId(String userId) {
        this.userId = userId;
    }

    public String getTaskId() {
        return taskId;
    }

    public void setTaskId(String taskId) {
        this.taskId = taskId;
    }

    public Object getFunctionParam() {
        return functionParam;
    }

    public void setFunctionParam(Object functionParam) {
        this.functionParam = functionParam;
    }

    /**
     * 耗材信息
     */
    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class Labware {
        @JsonProperty("LabwareName")
        private String labwareName;

        @JsonProperty("capacity")
        private String capacity;

        @JsonProperty("capacityRow")
        private int capacityRow;

        @JsonProperty("capacityColumn")
        private int capacityColumn;

        // Getters and Setters
        public String getLabwareName() {
            return labwareName;
        }

        public void setLabwareName(String labwareName) {
            this.labwareName = labwareName;
        }

        public String getCapacity() {
            return capacity;
        }

        public void setCapacity(String capacity) {
            this.capacity = capacity;
        }

        public int getCapacityRow() {
            return capacityRow;
        }

        public void setCapacityRow(int capacityRow) {
            this.capacityRow = capacityRow;
        }

        public int getCapacityColumn() {
            return capacityColumn;
        }

        public void setCapacityColumn(int capacityColumn) {
            this.capacityColumn = capacityColumn;
        }
    }
}

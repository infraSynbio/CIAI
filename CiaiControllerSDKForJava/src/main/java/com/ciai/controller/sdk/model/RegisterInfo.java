package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.List;

/**
 * 注册信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RegisterInfo {

    @JsonProperty("basicInfo")
    private BasicInfo basicInfo;

    @JsonProperty("advancedInfo")
    private AdvancedInfo advancedInfo;

    public BasicInfo getBasicInfo() {
        return basicInfo;
    }

    public void setBasicInfo(BasicInfo basicInfo) {
        this.basicInfo = basicInfo;
    }

    public AdvancedInfo getAdvancedInfo() {
        return advancedInfo;
    }

    public void setAdvancedInfo(AdvancedInfo advancedInfo) {
        this.advancedInfo = advancedInfo;
    }

    /**
     * 基础信息
     */
    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class BasicInfo {
        @JsonProperty("equipmentName")
        private String equipmentName;

        @JsonProperty("equipmentNameEN")
        private String equipmentNameEN;

        @JsonProperty("equipmentModel")
        private String equipmentModel;

        @JsonProperty("equipmentManufacturer")
        private String equipmentManufacturer;

        @JsonProperty("author")
        private String author;

        @JsonProperty("version")
        private String version;

        @JsonProperty("equipmentClass")
        private String equipmentClass;

        @JsonProperty("canE_Stop")
        private int canEmergencyStop;

        @JsonProperty("functionalResources")
        private int functionalResources;

        @JsonProperty("runtimeAccessibility")
        private int runtimeAccessibility;

        @JsonProperty("parallelizability")
        private int parallelizability;

        @JsonProperty("equipmentIcon")
        private String equipmentIcon;

        @JsonProperty("equipmentType")
        private int equipmentType;

        // Getters and Setters
        public String getEquipmentName() {
            return equipmentName;
        }

        public void setEquipmentName(String equipmentName) {
            this.equipmentName = equipmentName;
        }

        public String getEquipmentNameEN() {
            return equipmentNameEN;
        }

        public void setEquipmentNameEN(String equipmentNameEN) {
            this.equipmentNameEN = equipmentNameEN;
        }

        public String getEquipmentModel() {
            return equipmentModel;
        }

        public void setEquipmentModel(String equipmentModel) {
            this.equipmentModel = equipmentModel;
        }

        public String getEquipmentManufacturer() {
            return equipmentManufacturer;
        }

        public void setEquipmentManufacturer(String equipmentManufacturer) {
            this.equipmentManufacturer = equipmentManufacturer;
        }

        public String getAuthor() {
            return author;
        }

        public void setAuthor(String author) {
            this.author = author;
        }

        public String getVersion() {
            return version;
        }

        public void setVersion(String version) {
            this.version = version;
        }

        public String getEquipmentClass() {
            return equipmentClass;
        }

        public void setEquipmentClass(String equipmentClass) {
            this.equipmentClass = equipmentClass;
        }

        public int getCanEmergencyStop() {
            return canEmergencyStop;
        }

        public void setCanEmergencyStop(int canEmergencyStop) {
            this.canEmergencyStop = canEmergencyStop;
        }

        public int getFunctionalResources() {
            return functionalResources;
        }

        public void setFunctionalResources(int functionalResources) {
            this.functionalResources = functionalResources;
        }

        public int getRuntimeAccessibility() {
            return runtimeAccessibility;
        }

        public void setRuntimeAccessibility(int runtimeAccessibility) {
            this.runtimeAccessibility = runtimeAccessibility;
        }

        public int getParallelizability() {
            return parallelizability;
        }

        public void setParallelizability(int parallelizability) {
            this.parallelizability = parallelizability;
        }

        public String getEquipmentIcon() {
            return equipmentIcon;
        }

        public void setEquipmentIcon(String equipmentIcon) {
            this.equipmentIcon = equipmentIcon;
        }

        public int getEquipmentType() {
            return equipmentType;
        }

        public void setEquipmentType(int equipmentType) {
            this.equipmentType = equipmentType;
        }
    }

    /**
     * 高级信息
     */
    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class AdvancedInfo {
        @JsonProperty("equipmentFunctions")
        private List<EquipmentFunction> equipmentFunctions;

        @JsonProperty("equipmentGetInfos")
        private List<EquipmentGetInfo> equipmentGetInfos;

        @JsonProperty("equipmentSetInfos")
        private List<EquipmentSetInfo> equipmentSetInfos;

        @JsonProperty("equipmentNests")
        private List<EquipmentNest> equipmentNests;

        @JsonProperty("equipmentOperations")
        private List<EquipmentOperation> equipmentOperations;

        @JsonProperty("equipmentEnterAndExit")
        private EquipmentEnterAndExit equipmentEnterAndExit;

        // Getters and Setters
        public List<EquipmentFunction> getEquipmentFunctions() {
            return equipmentFunctions;
        }

        public void setEquipmentFunctions(List<EquipmentFunction> equipmentFunctions) {
            this.equipmentFunctions = equipmentFunctions;
        }

        public List<EquipmentGetInfo> getEquipmentGetInfos() {
            return equipmentGetInfos;
        }

        public void setEquipmentGetInfos(List<EquipmentGetInfo> equipmentGetInfos) {
            this.equipmentGetInfos = equipmentGetInfos;
        }

        public List<EquipmentSetInfo> getEquipmentSetInfos() {
            return equipmentSetInfos;
        }

        public void setEquipmentSetInfos(List<EquipmentSetInfo> equipmentSetInfos) {
            this.equipmentSetInfos = equipmentSetInfos;
        }

        public List<EquipmentNest> getEquipmentNests() {
            return equipmentNests;
        }

        public void setEquipmentNests(List<EquipmentNest> equipmentNests) {
            this.equipmentNests = equipmentNests;
        }

        public List<EquipmentOperation> getEquipmentOperations() {
            return equipmentOperations;
        }

        public void setEquipmentOperations(List<EquipmentOperation> equipmentOperations) {
            this.equipmentOperations = equipmentOperations;
        }

        public EquipmentEnterAndExit getEquipmentEnterAndExit() {
            return equipmentEnterAndExit;
        }

        public void setEquipmentEnterAndExit(EquipmentEnterAndExit equipmentEnterAndExit) {
            this.equipmentEnterAndExit = equipmentEnterAndExit;
        }
    }
}

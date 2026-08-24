package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * 设备位置信息
 */
public class EquipmentNest {

    @JsonProperty("nestName")
    private String nestName;

    @JsonProperty("labwareType")
    private String labwareType;

    @JsonProperty("nestPostures")
    private String nestPostures;

    @JsonProperty("postEnterFormJsonStructure")
    private String postEnterFormJsonStructure;

    @JsonProperty("preEnterFormJsonStructure")
    private String preEnterFormJsonStructure;

    @JsonProperty("postExitFormJsonStructure")
    private String postExitFormJsonStructure;

    @JsonProperty("preExitFormJsonStructure")
    private String preExitFormJsonStructure;

    @JsonProperty("nestAccessibility")
    private int nestAccessibility;

    @JsonProperty("nestDescription")
    private String nestDescription;

    @JsonProperty("nestHeight")
    private float nestHeight;

    @JsonProperty("nestCoordinate")
    private String nestCoordinate;

    @JsonProperty("nestColumnOrder")
    private int nestColumnOrder;

    @JsonProperty("nestColumnCo")
    private Integer nestColumnCo;

    @JsonProperty("nestLayerCo")
    private Integer nestLayerCo;

    @JsonProperty("typeOnly")
    private int typeOnly;

    @JsonProperty("nestIsDestination")
    private int nestIsDestination;

    @JsonProperty("transitionNest")
    private String transitionNest;

    // Getters and Setters
    public String getNestName() {
        return nestName;
    }

    public void setNestName(String nestName) {
        this.nestName = nestName;
    }

    public String getLabwareType() {
        return labwareType;
    }

    public void setLabwareType(String labwareType) {
        this.labwareType = labwareType;
    }

    public String getNestPostures() {
        return nestPostures;
    }

    public void setNestPostures(String nestPostures) {
        this.nestPostures = nestPostures;
    }

    public String getPostEnterFormJsonStructure() {
        return postEnterFormJsonStructure;
    }

    public void setPostEnterFormJsonStructure(String postEnterFormJsonStructure) {
        this.postEnterFormJsonStructure = postEnterFormJsonStructure;
    }

    public String getPreEnterFormJsonStructure() {
        return preEnterFormJsonStructure;
    }

    public void setPreEnterFormJsonStructure(String preEnterFormJsonStructure) {
        this.preEnterFormJsonStructure = preEnterFormJsonStructure;
    }

    public String getPostExitFormJsonStructure() {
        return postExitFormJsonStructure;
    }

    public void setPostExitFormJsonStructure(String postExitFormJsonStructure) {
        this.postExitFormJsonStructure = postExitFormJsonStructure;
    }

    public String getPreExitFormJsonStructure() {
        return preExitFormJsonStructure;
    }

    public void setPreExitFormJsonStructure(String preExitFormJsonStructure) {
        this.preExitFormJsonStructure = preExitFormJsonStructure;
    }

    public int getNestAccessibility() {
        return nestAccessibility;
    }

    public void setNestAccessibility(int nestAccessibility) {
        this.nestAccessibility = nestAccessibility;
    }

    public String getNestDescription() {
        return nestDescription;
    }

    public void setNestDescription(String nestDescription) {
        this.nestDescription = nestDescription;
    }

    public float getNestHeight() {
        return nestHeight;
    }

    public void setNestHeight(float nestHeight) {
        this.nestHeight = nestHeight;
    }

    public String getNestCoordinate() {
        return nestCoordinate;
    }

    public void setNestCoordinate(String nestCoordinate) {
        this.nestCoordinate = nestCoordinate;
    }

    public int getNestColumnOrder() {
        return nestColumnOrder;
    }

    public void setNestColumnOrder(int nestColumnOrder) {
        this.nestColumnOrder = nestColumnOrder;
    }

    public Integer getNestColumnCo() {
        return nestColumnCo;
    }

    public void setNestColumnCo(Integer nestColumnCo) {
        this.nestColumnCo = nestColumnCo;
    }

    public Integer getNestLayerCo() {
        return nestLayerCo;
    }

    public void setNestLayerCo(Integer nestLayerCo) {
        this.nestLayerCo = nestLayerCo;
    }

    public int getTypeOnly() {
        return typeOnly;
    }

    public void setTypeOnly(int typeOnly) {
        this.typeOnly = typeOnly;
    }

    public int getNestIsDestination() {
        return nestIsDestination;
    }

    public void setNestIsDestination(int nestIsDestination) {
        this.nestIsDestination = nestIsDestination;
    }

    public String getTransitionNest() {
        return transitionNest;
    }

    public void setTransitionNest(String transitionNest) {
        this.transitionNest = transitionNest;
    }
}

package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;

/**
 * 心跳信息
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class HeartBeatInfo {

    @JsonProperty("heartBeatStatus")
    private int heartBeatStatus;

    @JsonProperty("heartBeatTime")
    private String heartBeatTime;

    public HeartBeatInfo() {
        this.heartBeatTime = OffsetDateTime.now().format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
    }

    public HeartBeatInfo(int heartBeatStatus) {
        this.heartBeatStatus = heartBeatStatus;
        this.heartBeatTime = OffsetDateTime.now().format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
    }

    public HeartBeatInfo(HeartBeatStatus status) {
        this.heartBeatStatus = status.getValue();
        this.heartBeatTime = OffsetDateTime.now().format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
    }

    // Static factory methods
    public static HeartBeatInfo normal() {
        return new HeartBeatInfo(HeartBeatStatus.Normal);
    }

    public static HeartBeatInfo driverAbnormal() {
        return new HeartBeatInfo(HeartBeatStatus.DriverAbnormal);
    }

    public static HeartBeatInfo driverOverTime() {
        return new HeartBeatInfo(HeartBeatStatus.DriverOverTime);
    }

    public static HeartBeatInfo equipmentAbnormal() {
        return new HeartBeatInfo(HeartBeatStatus.EquipmentAbnormal);
    }

    public static HeartBeatInfo equipmentError() {
        return new HeartBeatInfo(HeartBeatStatus.EquipmentError);
    }

    public static HeartBeatInfo equipmentOverTime() {
        return new HeartBeatInfo(HeartBeatStatus.EquipmentOverTime);
    }

    public static HeartBeatInfo monitoring() {
        return new HeartBeatInfo(HeartBeatStatus.Monitoring);
    }

    // Getters and Setters
    public int getHeartBeatStatus() {
        return heartBeatStatus;
    }

    public void setHeartBeatStatus(int heartBeatStatus) {
        this.heartBeatStatus = heartBeatStatus;
    }

    public void setHeartBeatStatus(HeartBeatStatus status) {
        this.heartBeatStatus = status.getValue();
    }

    public String getHeartBeatTime() {
        return heartBeatTime;
    }

    public void setHeartBeatTime(String heartBeatTime) {
        this.heartBeatTime = heartBeatTime;
    }
}

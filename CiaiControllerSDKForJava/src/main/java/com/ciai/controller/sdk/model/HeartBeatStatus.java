package com.ciai.controller.sdk.model;

/**
 * 心跳状态枚举
 */
public enum HeartBeatStatus {
    /**
     * 正常
     */
    Normal(0),

    /**
     * 驱动异常
     */
    DriverAbnormal(1),

    /**
     * 驱动超时
     */
    DriverOverTime(2),

    /**
     * 设备异常
     */
    EquipmentAbnormal(3),

    /**
     * 设备错误
     */
    EquipmentError(4),

    /**
     * 设备超时
     */
    EquipmentOverTime(5),

    /**
     * 监控中
     */
    Monitoring(6);

    private final int value;

    HeartBeatStatus(int value) {
        this.value = value;
    }

    public int getValue() {
        return value;
    }

    public static HeartBeatStatus fromValue(int value) {
        for (HeartBeatStatus status : values()) {
            if (status.value == value) {
                return status;
            }
        }
        return Normal;
    }
}

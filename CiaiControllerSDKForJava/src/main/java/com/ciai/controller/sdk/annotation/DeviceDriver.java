package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备驱动类标记注解
 * 用于标记设备驱动类，提供设备的基本信息
 */
@Target(ElementType.TYPE)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceDriver {
    /**
     * 设备名称
     */
    String name();

    /**
     * 设备英文名称
     */
    String nameEN() default "";

    /**
     * 设备型号
     */
    String model() default "";

    /**
     * 设备制造商
     */
    String manufacturer() default "";

    /**
     * 驱动版本
     */
    String version() default "1.0.0";

    /**
     * 设备类型: 1-核心 2-转移 3-辅助 4-存储
     */
    int equipmentType() default 1;

    /**
     * 功能资源数
     */
    int functionalResources() default 1;

    /**
     * 是否支持急停
     */
    boolean canEmergencyStop() default true;

    /**
     * 运行时可访问性
     */
    int runtimeAccessibility() default 1;

    /**
     * 可并行性
     */
    int parallelizability() default 0;

    /**
     * 设备图标Base64
     */
    String icon() default "";

    /**
     * 设备图标文件名
     */
    String iconFile() default "";

    /**
     * 驱动作者
     */
    String author() default "";

    /**
     * 设备分类
     */
    String equipmentClass() default "";
}

package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备状态获取方法标记注解
 * 用于标记设备的状态获取方法
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceGet {
    /**
     * 状态名称
     */
    String name();

    /**
     * 中文标题
     */
    String titleCN() default "";

    /**
     * 英文标题
     */
    String titleEN() default "";

    /**
     * 类型: boolean, string, int, float
     */
    String type() default "string";

    /**
     * 单位
     */
    String unit() default "";

    /**
     * 描述
     */
    String description() default "";
}

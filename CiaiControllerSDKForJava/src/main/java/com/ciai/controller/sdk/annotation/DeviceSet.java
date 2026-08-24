package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备设置方法标记注解
 * 用于标记设备的参数设置方法
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceSet {
    /**
     * 参数名称
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
     * 参数类型: input, select
     */
    String type() default "input";

    /**
     * 参数单位
     */
    String unit() default "";

    /**
     * 参数描述
     */
    String description() default "";
}

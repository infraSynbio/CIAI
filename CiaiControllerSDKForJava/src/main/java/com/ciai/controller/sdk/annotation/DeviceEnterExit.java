package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备进出操作方法标记注解
 * 用于标记设备的进出操作方法
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceEnterExit {
    /**
     * 进出操作名称
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
     * 描述
     */
    String description() default "";
}

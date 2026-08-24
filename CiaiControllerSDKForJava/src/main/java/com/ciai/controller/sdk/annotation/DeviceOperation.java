package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备操作方法标记注解
 * 用于标记设备的操作方法
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceOperation {
    /**
     * 操作名称
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
     * 操作描述
     */
    String description() default "";

    /**
     * 表单JSON结构
     */
    String formJson() default "";
}

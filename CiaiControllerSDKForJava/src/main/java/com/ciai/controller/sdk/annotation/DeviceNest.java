package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备位置属性标记注解
 * 用于标记设备的位置属性（getter方法）
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceNest {
    /**
     * 位置顺序（用于排序）
     */
    int order() default 0;
}

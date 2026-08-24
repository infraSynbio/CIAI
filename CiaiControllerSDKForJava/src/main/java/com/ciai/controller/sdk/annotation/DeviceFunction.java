package com.ciai.controller.sdk.annotation;

import java.lang.annotation.*;

/**
 * 设备功能方法标记注解
 * 用于标记设备的功能方法
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface DeviceFunction {
    /**
     * 功能名称
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
     * 功能描述
     */
    String description() default "";

    /**
     * 中文分类
     */
    String categoryCN() default "";

    /**
     * 英文分类
     */
    String categoryEN() default "";

    /**
     * 默认执行时间(秒)
     */
    int defaultPeriod() default 60;

    /**
     * 动态表单JSON定义
     */
    String formJson() default "";

    /**
     * 黑色图标Base64
     */
    String iconBlack() default "";

    /**
     * 白色图标Base64
     */
    String iconWhite() default "";

    /**
     * 黑色图标文件名
     */
    String iconFileBlack() default "";

    /**
     * 白色图标文件名
     */
    String iconFileWhite() default "";
}

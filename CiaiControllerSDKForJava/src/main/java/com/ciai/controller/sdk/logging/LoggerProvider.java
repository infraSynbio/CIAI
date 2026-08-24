package com.ciai.controller.sdk.logging;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * 日志提供器
 */
public final class LoggerProvider {

    private static org.slf4j.ILoggerFactory loggerFactory;

    static {
        // 默认使用SLF4J的默认实现
        loggerFactory = LoggerFactory.getILoggerFactory();
    }

    private LoggerProvider() {
        // 私有构造函数，防止实例化
    }

    /**
     * 获取日志工厂
     */
    public static org.slf4j.ILoggerFactory getFactory() {
        return loggerFactory;
    }

    /**
     * 设置自定义日志工厂
     */
    public static void setLoggerFactory(org.slf4j.ILoggerFactory factory) {
        if (factory != null) {
            loggerFactory = factory;
        }
    }

    /**
     * 创建日志器
     */
    public static Logger createLogger(Class<?> clazz) {
        return loggerFactory.getLogger(clazz.getName());
    }

    /**
     * 创建日志器
     */
    public static Logger createLogger(String categoryName) {
        return loggerFactory.getLogger(categoryName);
    }

    /**
     * 重置为默认
     */
    public static void resetToDefault() {
        loggerFactory = LoggerFactory.getILoggerFactory();
    }
}

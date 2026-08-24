package com.ciai.controller.sdk.service;

import com.ciai.controller.sdk.communication.HttpCommunication;
import com.ciai.controller.sdk.communication.SerialCommunication;
import com.ciai.controller.sdk.communication.TcpCommunication;
import com.ciai.controller.sdk.core.CommunicationType;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.interface_.ICommunication;

import java.nio.charset.Charset;

/**
 * 根据统一设备配置创建SDK内置通信对象。DLL/API驱动返回null。
 */
public final class CommunicationFactory {

    private CommunicationFactory() {
    }

    public static ICommunication create(DeviceConfiguration config) {
        validate(config);
        switch (config.getCommunicationType()) {
            case TCP:
                return new TcpCommunication(config.getHost(), config.getPort(),
                        config.getConnectionTimeout(), config.getReadTimeout(), config.getWriteTimeout());
            case HTTP:
                return new HttpCommunication(config.getBaseUrl(), config.getConnectionTimeout());
            case SERIAL:
                return new SerialCommunication(config.getSerialPort(), config.getBaudRate(),
                        config.getDataBits(), parseStopBits(config.getStopBits()),
                        parseParity(config.getParity()), config.getReadTimeout(),
                        config.getWriteTimeout(), Charset.forName(config.getEncoding()));
            case DLL:
                return null;
            default:
                throw new IllegalArgumentException("Unsupported communication type: "
                        + config.getCommunicationType());
        }
    }

    public static boolean canCreate(DeviceConfiguration config) {
        if (config == null || config.getCommunicationType() == null) {
            return false;
        }
        switch (config.getCommunicationType()) {
            case TCP:
                return notBlank(config.getHost()) && config.getPort() > 0 && config.getPort() <= 65535;
            case HTTP:
                return notBlank(config.getBaseUrl());
            case SERIAL:
                return notBlank(config.getSerialPort());
            case DLL:
                return true;
            default:
                return false;
        }
    }

    public static void validate(DeviceConfiguration config) {
        if (config == null) {
            throw new IllegalArgumentException("Device configuration is required");
        }
        if (!canCreate(config)) {
            throw new IllegalArgumentException("Incomplete " + config.getCommunicationType()
                    + " communication configuration");
        }
        if (config.getConnectionTimeout() <= 0 || config.getReadTimeout() <= 0
                || config.getWriteTimeout() <= 0) {
            throw new IllegalArgumentException("Communication timeouts must be greater than zero");
        }
        if (config.getDeviceCallResources() <= 0 || config.getDeviceCallTimeout() <= 0) {
            throw new IllegalArgumentException("Device call resources and timeout must be greater than zero");
        }
    }

    private static int parseParity(String value) {
        String normalized = value == null ? "none" : value.trim().toLowerCase();
        switch (normalized) {
            case "none": return SerialCommunication.PARITY_NONE;
            case "odd": return SerialCommunication.PARITY_ODD;
            case "even": return SerialCommunication.PARITY_EVEN;
            case "mark": return SerialCommunication.PARITY_MARK;
            case "space": return SerialCommunication.PARITY_SPACE;
            default: throw new IllegalArgumentException("Unsupported serial parity: " + value);
        }
    }

    private static int parseStopBits(double value) {
        if (Math.abs(value - 1) < 0.001) return SerialCommunication.STOPBITS_ONE;
        if (Math.abs(value - 1.5) < 0.001) return SerialCommunication.STOPBITS_ONE_POINT_FIVE;
        if (Math.abs(value - 2) < 0.001) return SerialCommunication.STOPBITS_TWO;
        throw new IllegalArgumentException("Unsupported serial stop bits: " + value);
    }

    private static boolean notBlank(String value) {
        return value != null && !value.trim().isEmpty();
    }
}

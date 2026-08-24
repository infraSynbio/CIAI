package com.ciai.controller.sdk.interface_;

import com.ciai.controller.sdk.core.ConnectionConfiguration;
import java.util.Collection;

/** 厂商协议或第三方通信实现的注册入口。 */
public interface ICommunicationProvider {
    Collection<String> getTypes();
    void validate(ConnectionConfiguration configuration);
    ICommunication create(ConnectionConfiguration configuration);
}

package com.ciai.examples.temperature;

import com.ciai.controller.sdk.webserver.DriverHost;

public final class Main {
    private Main() {
    }

    public static void main(String[] args) throws Exception {
        DriverHost.run(TemperatureDriver.class, "application.yml");
    }
}

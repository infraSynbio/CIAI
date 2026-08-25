package com.ciai.examples.temperature;

import com.ciai.controller.sdk.webserver.DriverCli;

public final class Main {
    private Main() {
    }

    public static void main(String[] args) {
        DriverCli.run(TemperatureDriver.class, args);
    }
}

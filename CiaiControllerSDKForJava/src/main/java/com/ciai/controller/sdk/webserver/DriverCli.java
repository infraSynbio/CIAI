package com.ciai.controller.sdk.webserver;

import com.ciai.controller.sdk.config.ConfigurationValidationReport;
import com.ciai.controller.sdk.core.DeviceDriverBase;

/** Standard executable entry point: positional config path, --config and --validate. */
public final class DriverCli {
    private DriverCli() {
    }

    public static <T extends DeviceDriverBase> void run(Class<T> driverClass, String[] args) {
        Options options = parse(args);
        if (options.validateOnly) {
            ConfigurationValidationReport report = DriverHost.validateConfiguration(
                    driverClass, options.configPath);
            if (!report.getDiagnostics().isEmpty()) {
                System.out.println(report.formatDiagnostics());
            }
            report.throwIfInvalid();
            System.out.println("Configuration is valid: " + report.getConfigPath());
            return;
        }
        DriverHost.run(driverClass, options.configPath);
    }

    private static Options parse(String[] args) {
        Options result = new Options();
        if (args == null) return result;
        for (int index = 0; index < args.length; index++) {
            String value = args[index];
            if ("--validate".equals(value) || "-v".equals(value)) {
                result.validateOnly = true;
            } else if ("--config".equals(value) || "-c".equals(value)) {
                if (++index >= args.length) throw new IllegalArgumentException(value + " requires a path");
                result.configPath = args[index];
            } else if (value != null && !value.startsWith("-")
                    && "application.yml".equals(result.configPath)) {
                result.configPath = value;
            } else {
                throw new IllegalArgumentException("Unknown argument: " + value);
            }
        }
        return result;
    }

    private static final class Options {
        private String configPath = "application.yml";
        private boolean validateOnly;
    }
}

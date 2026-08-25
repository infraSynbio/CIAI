package com.ciai.controller.sdk.config;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** A device-free configuration preflight result for installers, CI and command-line checks. */
public final class ConfigurationValidationReport {
    private final String configPath;
    private final List<ConfigurationDiagnostic> diagnostics;

    public ConfigurationValidationReport(String configPath, List<ConfigurationDiagnostic> diagnostics) {
        this.configPath = configPath;
        this.diagnostics = Collections.unmodifiableList(new ArrayList<>(diagnostics));
    }

    public String getConfigPath() { return configPath; }
    public List<ConfigurationDiagnostic> getDiagnostics() { return diagnostics; }
    public boolean isValid() {
        for (ConfigurationDiagnostic diagnostic : diagnostics) {
            if (diagnostic.getSeverity() == ConfigurationDiagnostic.Severity.ERROR) return false;
        }
        return true;
    }
    public boolean hasWarnings() {
        for (ConfigurationDiagnostic diagnostic : diagnostics) {
            if (diagnostic.getSeverity() == ConfigurationDiagnostic.Severity.WARNING) return true;
        }
        return false;
    }
    public void throwIfInvalid() {
        if (!isValid()) throw new IllegalArgumentException(formatDiagnostics());
    }
    public String formatDiagnostics() {
        StringBuilder result = new StringBuilder();
        for (ConfigurationDiagnostic diagnostic : diagnostics) {
            if (result.length() > 0) result.append(System.lineSeparator());
            result.append(diagnostic);
        }
        return result.toString();
    }
}

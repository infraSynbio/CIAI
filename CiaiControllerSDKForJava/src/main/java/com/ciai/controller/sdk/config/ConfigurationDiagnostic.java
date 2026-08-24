package com.ciai.controller.sdk.config;

public class ConfigurationDiagnostic {
    public enum Severity { INFO, WARNING, ERROR }
    private final Severity severity; private final String path; private final String message;
    public ConfigurationDiagnostic(Severity severity,String path,String message){this.severity=severity;this.path=path;this.message=message;}
    public Severity getSeverity(){return severity;} public String getPath(){return path;} public String getMessage(){return message;}
    public String toString(){return severity+": "+path+": "+message;}
}

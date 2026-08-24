package com.ciai.controller.sdk.config;

import com.ciai.controller.sdk.core.ConnectionConfiguration;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.service.CommunicationProviderRegistry;
import java.util.*;

/** 启动前配置体检，错误精确到YAML路径。 */
public final class ConfigurationValidator {
    private ConfigurationValidator(){}
    public static List<ConfigurationDiagnostic> validate(DeviceConfiguration configuration){
        List<ConfigurationDiagnostic> result=new ArrayList<>();
        if(configuration==null){result.add(error("device","Device configuration is required"));return result;}
        if(configuration.getDeviceCallResources()<=0)result.add(error("device.deviceCallResources","Must be greater than zero"));
        if(configuration.getDeviceCallTimeout()<=0)result.add(error("device.deviceCallTimeoutMs","Must be greater than zero"));
        Collection<ConnectionConfiguration> connections=configuration.getConnections().values();
        int defaults=0;Map<String,Integer> groupLimits=new HashMap<>();
        for(ConnectionConfiguration c:connections){
            if(c.isDefault())defaults++;String path="device.connections."+c.getName();
            try{CommunicationProviderRegistry.validate(c);}catch(RuntimeException e){result.add(error(path,e.getMessage()));}
            if(c.getEffectiveMaxConcurrency()==1&&c.getMaxConcurrency()>1)result.add(warning(path+".maxConcurrency","Single-channel transport is always serialized"));
            if(c.getResourceGroup()!=null&&!c.getResourceGroup().trim().isEmpty()){
                String group=c.getResourceGroup().toLowerCase(Locale.ROOT);Integer old=groupLimits.putIfAbsent(group,c.getEffectiveMaxConcurrency());
                if(old!=null&&old!=c.getEffectiveMaxConcurrency())result.add(error("device.connections","Resource group "+c.getResourceGroup()+" must use one maxConcurrency"));
            }
        }
        if(defaults>1)result.add(error("device.connections","Only one default connection is allowed"));
        return result;
    }
    public static void validateAndThrow(DeviceConfiguration configuration){
        StringBuilder message=new StringBuilder();for(ConfigurationDiagnostic d:validate(configuration))if(d.getSeverity()==ConfigurationDiagnostic.Severity.ERROR)message.append(d).append('\n');
        if(message.length()>0)throw new IllegalArgumentException(message.toString().trim());
    }
    private static ConfigurationDiagnostic error(String p,String m){return new ConfigurationDiagnostic(ConfigurationDiagnostic.Severity.ERROR,p,m);}
    private static ConfigurationDiagnostic warning(String p,String m){return new ConfigurationDiagnostic(ConfigurationDiagnostic.Severity.WARNING,p,m);}
}

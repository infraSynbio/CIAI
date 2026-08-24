package com.ciai.controller.sdk.core;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** 一个可命名的底层连接。 */
public class ConnectionConfiguration {
    private String name = "default";
    private String type = "tcp";
    private boolean isDefault;
    private boolean required = true;
    private boolean connectOnStart = true;
    private String host;
    private int port;
    private String baseUrl;
    private String serialPort;
    private int baudRate = 9600;
    private int dataBits = 8;
    private double stopBits = 1;
    private String parity = "none";
    private String encoding = "utf-8";
    private String flowControl = "none";
    private boolean dtrEnable;
    private boolean rtsEnable;
    private boolean discardInputBeforeWrite;
    private int connectTimeoutMs = 5000;
    private int readTimeoutMs = 10000;
    private int writeTimeoutMs = 10000;
    private int resourceWaitTimeoutMs = 30000;
    private int maxConcurrency = 1;
    private String resourceGroup;
    private int retryCount;
    private int retryDelayMs = 200;
    private double retryBackoff = 2;
    private String executable;
    private List<String> arguments = new ArrayList<>();
    private String workingDirectory;
    private String architecture = "auto";
    private String framework;
    private String apartmentState = "MTA";
    private int shutdownTimeoutMs = 5000;
    private Map<String, String> environment = new LinkedHashMap<>();
    private Map<String, String> headers = new LinkedHashMap<>();
    private Map<String, Object> settings = new LinkedHashMap<>();

    public int getEffectiveMaxConcurrency() {
        String value = type == null ? "" : type.trim().toLowerCase();
        if (value.equals("tcp") || value.equals("serial") || value.equals("modbus")
                || value.equals("modbus-tcp") || value.equals("modbus-rtu")) return 1;
        return Math.max(1, maxConcurrency);
    }
    public String getName(){return name;} public void setName(String v){name=v;}
    public String getType(){return type;} public void setType(String v){type=v;}
    public boolean isDefault(){return isDefault;} public void setDefault(boolean v){isDefault=v;}
    public boolean isRequired(){return required;} public void setRequired(boolean v){required=v;}
    public boolean isConnectOnStart(){return connectOnStart;} public void setConnectOnStart(boolean v){connectOnStart=v;}
    public String getHost(){return host;} public void setHost(String v){host=v;}
    public int getPort(){return port;} public void setPort(int v){port=v;}
    public String getBaseUrl(){return baseUrl;} public void setBaseUrl(String v){baseUrl=v;}
    public String getSerialPort(){return serialPort;} public void setSerialPort(String v){serialPort=v;}
    public int getBaudRate(){return baudRate;} public void setBaudRate(int v){baudRate=v;}
    public int getDataBits(){return dataBits;} public void setDataBits(int v){dataBits=v;}
    public double getStopBits(){return stopBits;} public void setStopBits(double v){stopBits=v;}
    public String getParity(){return parity;} public void setParity(String v){parity=v;}
    public String getEncoding(){return encoding;} public void setEncoding(String v){encoding=v;}
    public String getFlowControl(){return flowControl;} public void setFlowControl(String v){flowControl=v;}
    public boolean isDtrEnable(){return dtrEnable;} public void setDtrEnable(boolean v){dtrEnable=v;}
    public boolean isRtsEnable(){return rtsEnable;} public void setRtsEnable(boolean v){rtsEnable=v;}
    public boolean isDiscardInputBeforeWrite(){return discardInputBeforeWrite;} public void setDiscardInputBeforeWrite(boolean v){discardInputBeforeWrite=v;}
    public int getConnectTimeoutMs(){return connectTimeoutMs;} public void setConnectTimeoutMs(int v){connectTimeoutMs=v;}
    public int getReadTimeoutMs(){return readTimeoutMs;} public void setReadTimeoutMs(int v){readTimeoutMs=v;}
    public int getWriteTimeoutMs(){return writeTimeoutMs;} public void setWriteTimeoutMs(int v){writeTimeoutMs=v;}
    public int getResourceWaitTimeoutMs(){return resourceWaitTimeoutMs;} public void setResourceWaitTimeoutMs(int v){resourceWaitTimeoutMs=v;}
    public int getMaxConcurrency(){return maxConcurrency;} public void setMaxConcurrency(int v){maxConcurrency=v;}
    public String getResourceGroup(){return resourceGroup;} public void setResourceGroup(String v){resourceGroup=v;}
    public int getRetryCount(){return retryCount;} public void setRetryCount(int v){retryCount=v;}
    public int getRetryDelayMs(){return retryDelayMs;} public void setRetryDelayMs(int v){retryDelayMs=v;}
    public double getRetryBackoff(){return retryBackoff;} public void setRetryBackoff(double v){retryBackoff=v;}
    public String getExecutable(){return executable;} public void setExecutable(String v){executable=v;}
    public List<String> getArguments(){return arguments;} public void setArguments(List<String> v){arguments=v==null?new ArrayList<String>():v;}
    public String getWorkingDirectory(){return workingDirectory;} public void setWorkingDirectory(String v){workingDirectory=v;}
    public String getArchitecture(){return architecture;} public void setArchitecture(String v){architecture=v;}
    public String getFramework(){return framework;} public void setFramework(String v){framework=v;}
    public String getApartmentState(){return apartmentState;} public void setApartmentState(String v){apartmentState=v;}
    public int getShutdownTimeoutMs(){return shutdownTimeoutMs;} public void setShutdownTimeoutMs(int v){shutdownTimeoutMs=v;}
    public Map<String,String> getEnvironment(){return environment;} public void setEnvironment(Map<String,String> v){environment=v==null?new LinkedHashMap<String,String>():v;}
    public Map<String,String> getHeaders(){return headers;} public void setHeaders(Map<String,String> v){headers=v==null?new LinkedHashMap<String,String>():v;}
    public Map<String,Object> getSettings(){return settings;} public void setSettings(Map<String,Object> v){settings=v==null?new LinkedHashMap<String,Object>():v;}
}

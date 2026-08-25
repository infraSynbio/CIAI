package com.ciai.controller.sdk.service;

import com.ciai.controller.sdk.communication.HttpCommunication;
import com.ciai.controller.sdk.communication.ProcessCommunication;
import com.ciai.controller.sdk.communication.SerialCommunication;
import com.ciai.controller.sdk.communication.TcpCommunication;
import com.ciai.controller.sdk.core.ConnectionConfiguration;
import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.interface_.ICommunicationProvider;
import com.fazecast.jSerialComm.SerialPort;

import java.net.URI;
import java.io.File;
import java.nio.charset.Charset;
import java.util.Arrays;
import java.util.Collection;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/** 内置连接和厂商连接的线程安全注册表。 */
public final class CommunicationProviderRegistry {
    private static final Map<String, ICommunicationProvider> PROVIDERS = new ConcurrentHashMap<>();
    static {
        register(provider(Arrays.asList("tcp"), new Validator(){ public void validate(ConnectionConfiguration c){
            if (blank(c.getHost()) || c.getPort() <= 0 || c.getPort() > 65535)
                throw new IllegalArgumentException("TCP connection requires host and port: " + c.getName());
        }}, new Creator(){ public ICommunication create(ConnectionConfiguration c){
            return new TcpCommunication(c.getHost(), c.getPort(), c.getConnectTimeoutMs(),
                    c.getReadTimeoutMs(), c.getWriteTimeoutMs()); }}), false);
        register(provider(Arrays.asList("http", "https"), new Validator(){ public void validate(ConnectionConfiguration c){
            try {
                URI u = URI.create(c.getBaseUrl());
                String scheme = u.getScheme();
                if (scheme == null || !(scheme.equalsIgnoreCase("http") || scheme.equalsIgnoreCase("https")))
                    throw new Exception();
            }
            catch (Exception e) { throw new IllegalArgumentException("Invalid HTTP baseUrl: " + c.getName()); }
        }}, new Creator(){ public ICommunication create(ConnectionConfiguration c){
            return new HttpCommunication(c.getBaseUrl(), c.getConnectTimeoutMs(), c.getHeaders()); }}), false);
        register(provider(Arrays.asList("serial"), new Validator(){ public void validate(ConnectionConfiguration c){
            if (blank(c.getSerialPort())) throw new IllegalArgumentException("Serial port is required: " + c.getName());
            if (c.getBaudRate() <= 0) throw new IllegalArgumentException("Serial baudRate must be positive: " + c.getName());
            if (c.getDataBits() < 5 || c.getDataBits() > 8)
                throw new IllegalArgumentException("Serial dataBits must be between 5 and 8: " + c.getName());
            stopBits(c.getStopBits());
            parity(c.getParity());
            Charset.forName(c.getEncoding());
            flowControl(c.getFlowControl());
        }}, new Creator(){ public ICommunication create(ConnectionConfiguration c){
            return new SerialCommunication(c.getSerialPort(), c.getBaudRate(), c.getDataBits(),
                    stopBits(c.getStopBits()), parity(c.getParity()), c.getReadTimeoutMs(), c.getWriteTimeoutMs(),
                    Charset.forName(c.getEncoding()), flowControl(c.getFlowControl()), c.isDtrEnable(),
                    c.isRtsEnable(), c.isDiscardInputBeforeWrite()); }}), false);
        register(provider(Arrays.asList("process", "legacy-process", "dll-process"),
                new Validator(){ public void validate(ConnectionConfiguration c){
                    if (blank(c.getExecutable())) throw new IllegalArgumentException("Process executable is required: " + c.getName());
                    if (!blank(c.getWorkingDirectory()) && !new File(c.getWorkingDirectory()).isDirectory())
                        throw new IllegalArgumentException("Process workingDirectory does not exist: " + c.getWorkingDirectory());
                    if (hasPath(c.getExecutable()) && !new File(c.getExecutable()).isFile())
                        throw new IllegalArgumentException("Process executable does not exist: " + c.getExecutable());
                    String architecture = blank(c.getArchitecture()) ? "auto" : c.getArchitecture().trim().toLowerCase(Locale.ROOT);
                    if (!(architecture.equals("auto") || architecture.equals("x86") || architecture.equals("x64") || architecture.equals("arm64")))
                        throw new IllegalArgumentException("Invalid process architecture: " + c.getArchitecture());
                    String apartment = blank(c.getApartmentState()) ? "MTA" : c.getApartmentState().trim().toUpperCase(Locale.ROOT);
                    if (!(apartment.equals("STA") || apartment.equals("MTA")))
                        throw new IllegalArgumentException("Invalid process apartmentState: " + c.getApartmentState());
                    if (c.getShutdownTimeoutMs() <= 0)
                        throw new IllegalArgumentException("Process shutdownTimeoutMs must be positive: " + c.getName());
                }}, new Creator(){ public ICommunication create(ConnectionConfiguration c){ return new ProcessCommunication(c); }}), false);
    }
    private CommunicationProviderRegistry() {}

    public static void register(ICommunicationProvider provider) { register(provider, false); }
    public static void register(ICommunicationProvider provider, boolean replace) {
        if (provider == null) throw new IllegalArgumentException("Provider is required");
        for (String type : provider.getTypes()) {
            String key = type.trim().toLowerCase(Locale.ROOT);
            if (replace) PROVIDERS.put(key, provider);
            else if (PROVIDERS.putIfAbsent(key, provider) != null)
                throw new IllegalStateException("Communication type already registered: " + type);
        }
    }
    public static boolean isRegistered(String type) { return type != null && PROVIDERS.containsKey(type.trim().toLowerCase(Locale.ROOT)); }
    public static ICommunication create(ConnectionConfiguration c) {
        ICommunicationProvider provider = validate(c);
        return provider.create(c);
    }
    public static ICommunicationProvider validate(ConnectionConfiguration c) {
        if (c == null || blank(c.getType())) throw new IllegalArgumentException("Connection type is required");
        ICommunicationProvider provider = PROVIDERS.get(c.getType().trim().toLowerCase(Locale.ROOT));
        if (provider == null) throw new IllegalArgumentException("Unregistered communication type: " + c.getType());
        validateCommon(c); provider.validate(c); return provider;
    }
    private static void validateCommon(ConnectionConfiguration c) {
        if (blank(c.getName())) throw new IllegalArgumentException("Connection name is required");
        if (c.getConnectTimeoutMs() <= 0 || c.getReadTimeoutMs() <= 0 || c.getWriteTimeoutMs() <= 0
                || c.getResourceWaitTimeoutMs() <= 0) throw new IllegalArgumentException("Invalid timeout: " + c.getName());
        if (c.getMaxConcurrency() <= 0) throw new IllegalArgumentException("maxConcurrency must be positive: " + c.getName());
        if (c.getRetryCount() < 0 || c.getRetryDelayMs() < 0 || c.getRetryBackoff() < 1)
            throw new IllegalArgumentException("Invalid retry policy: " + c.getName());
    }
    private interface Validator { void validate(ConnectionConfiguration c); }
    private interface Creator { ICommunication create(ConnectionConfiguration c); }
    private static ICommunicationProvider provider(final Collection<String> types, final Validator v, final Creator cr) {
        return new ICommunicationProvider() {
            public Collection<String> getTypes(){return types;}
            public void validate(ConnectionConfiguration c){v.validate(c);}
            public ICommunication create(ConnectionConfiguration c){return cr.create(c);}
        };
    }
    private static boolean blank(String v){return v==null||v.trim().isEmpty();}
    private static boolean hasPath(String v){return v.indexOf('/')>=0||v.indexOf('\\')>=0||new File(v).isAbsolute();}
    private static int parity(String v){String x=v==null?"none":v.toLowerCase(Locale.ROOT); if(x.equals("odd"))return SerialPort.ODD_PARITY;if(x.equals("even"))return SerialPort.EVEN_PARITY;if(x.equals("mark"))return SerialPort.MARK_PARITY;if(x.equals("space"))return SerialPort.SPACE_PARITY;if(x.equals("none"))return SerialPort.NO_PARITY;throw new IllegalArgumentException("Invalid parity: "+v);}
    private static int stopBits(double v){if(Math.abs(v-1)<.001)return SerialPort.ONE_STOP_BIT;if(Math.abs(v-1.5)<.001)return SerialPort.ONE_POINT_FIVE_STOP_BITS;if(Math.abs(v-2)<.001)return SerialPort.TWO_STOP_BITS;throw new IllegalArgumentException("Invalid stop bits: "+v);}
    private static int flowControl(String v){String x=v==null?"none":v.toLowerCase(Locale.ROOT);if(x.equals("none"))return SerialPort.FLOW_CONTROL_DISABLED;if(x.equals("xonxoff")||x.equals("xon/xoff")||x.equals("software"))return SerialPort.FLOW_CONTROL_XONXOFF_IN_ENABLED|SerialPort.FLOW_CONTROL_XONXOFF_OUT_ENABLED;if(x.equals("rtscts")||x.equals("rts/cts")||x.equals("hardware"))return SerialPort.FLOW_CONTROL_RTS_ENABLED|SerialPort.FLOW_CONTROL_CTS_ENABLED;if(x.equals("both")||x.equals("rtscts+xonxoff"))return SerialPort.FLOW_CONTROL_RTS_ENABLED|SerialPort.FLOW_CONTROL_CTS_ENABLED|SerialPort.FLOW_CONTROL_XONXOFF_IN_ENABLED|SerialPort.FLOW_CONTROL_XONXOFF_OUT_ENABLED;throw new IllegalArgumentException("Invalid flow control: "+v);}
}

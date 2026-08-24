package com.ciai.controller.sdk;

import com.ciai.controller.sdk.annotation.*;
import com.ciai.controller.sdk.communication.TcpCommunication;
import com.ciai.controller.sdk.communication.HttpCommunication;
import com.ciai.controller.sdk.config.DriverConfig;
import com.ciai.controller.sdk.config.YamlConfigLoader;
import com.ciai.controller.sdk.core.CommunicationType;
import com.ciai.controller.sdk.core.DeviceConfiguration;
import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.core.ConnectionConfiguration;
import com.ciai.controller.sdk.service.ConnectionManager;
import com.ciai.controller.sdk.service.CommunicationProviderRegistry;
import com.ciai.controller.sdk.service.FileWorkflow;
import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.interface_.ICommunicationProvider;
import com.ciai.controller.sdk.model.*;
import com.ciai.controller.sdk.webserver.DriverHost;
import com.ciai.controller.sdk.webserver.DriverHttpServer;
import com.ciai.controller.sdk.webserver.HttpsOptions;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.io.InputStream;
import java.io.OutputStream;
import java.io.ByteArrayOutputStream;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.net.InetSocketAddress;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.OffsetDateTime;
import java.util.Arrays;
import java.util.Collections;
import java.util.Collection;
import java.util.List;
import java.util.ArrayList;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import com.sun.net.httpserver.HttpServer;

/** 可直接运行的Java SDK契约测试，不依赖真实设备。 */
public final class SdkContractTest {

    @Test
    public void contractSuite() throws Exception {
        main(new String[0]);
    }

    public static void main(String[] args) throws Exception {
        testYamlAndDeclarativeConfiguration();
        testJavaDispatchCorrectness();
        testContractValidation();
        testNamedConnections();
        testCancellationEventsAndDynamicNests();
        testFileWorkflow();
        testDeviceCallResources();
        testTcpTransactions();
        testTcpFraming();
        testHttpCommunication();
        testServerGuards();
        testServerBackpressure();
        testResultNullSerialization();
        System.out.println("CiaiControllerSDKForJava contract tests passed.");
    }

    private static void testYamlAndDeclarativeConfiguration() {
        String yaml = "server:\n"
                + "  port: 8080\n"
                + "  host: localhost\n"
                + "  useHttps: false\n"
                + "device:\n"
                + "  deviceId: java-contract\n"
                + "  communicationType: DLL\n"
                + "  deviceCallResources: 2\n"
                + "  deviceCallTimeoutMs: 4321\n"
                + "  simulated: true\n"
                + "  settings:\n"
                + "    retries: 3\n";
        DriverConfig parsed = YamlConfigLoader.parse(yaml);
        DeviceConfiguration config = YamlConfigLoader.toDeviceConfiguration(parsed);
        HttpsOptions serverOptions = YamlConfigLoader.toHttpsOptions(parsed);
        check("TLSv1.2".equals(serverOptions.getProtocol())
                        && Arrays.equals(new String[]{"TLSv1.2"}, serverOptions.getEnabledProtocols())
                        && serverOptions.getCiphers().length == 0,
                "portable TLS defaults were not preserved");
        check(config.getCommunicationType() == CommunicationType.DLL,
                "DLL communication type was not mapped");
        check(config.getDeviceCallResources() == 2 && config.getDeviceCallTimeout() == 4321,
                "device-call resource settings were not mapped");
        check(config.getExtraSetting("simulated", Boolean.class, false),
                "direct custom device setting was not captured");
        check(config.getExtraSetting("retries", Integer.class, 0) == 3,
                "device.settings value was not captured");

        DriverConfig namedParsed = YamlConfigLoader.parse("device:\n"
                + "  deviceId: named\n"
                + "  connections:\n"
                + "    control:\n"
                + "      type: tcp\n"
                + "      default: true\n"
                + "      host: 127.0.0.1\n"
                + "      port: 5000\n"
                + "      maxConcurrency: 9\n"
                + "    serialBus:\n"
                + "      type: serial\n"
                + "      serialPort: COM9\n"
                + "      flowControl: rtscts\n");
        DeviceConfiguration named = YamlConfigLoader.toDeviceConfiguration(namedParsed);
        check(named.getConnections().size() == 2 && named.getConnections().get("control").isDefault()
                        && "rtscts".equals(named.getConnections().get("serialBus").getFlowControl()),
                "named connections YAML mapping mismatch");
        check(named.getConnections().get("control").getEffectiveMaxConcurrency() == 1,
                "TCP YAML maxConcurrency must be forced to one");

        boolean missingEnvironmentRejected = false;
        try {
            YamlConfigLoader.parseOrThrow("device:\n  deviceId: ${CIAI_SDK_TEST_REQUIRED_ENV_91F4}\n");
        } catch (IllegalArgumentException expected) {
            missingEnvironmentRejected = expected.getMessage().contains("CIAI_SDK_TEST_REQUIRED_ENV_91F4");
        }
        check(missingEnvironmentRejected,
                "missing required environment variable did not produce an actionable error");

        DriverConfig httpWithHttpsTemplate = YamlConfigLoader.parseOrThrow("server:\n"
                + "  port: 8080\n"
                + "  useHttps: false\n"
                + "  # certificate:\n"
                + "  #   password: \"${CIAI_SDK_TEST_COMMENT_ONLY_MISSING}\"\n"
                + "device:\n"
                + "  communicationType: DLL\n");
        check(!httpWithHttpsTemplate.getServer().isUseHttps(),
                "HTTP configuration must ignore environment placeholders in HTTPS comments");

        DeclarativeDriver driver = DriverHost.createDriver(DeclarativeDriver.class, config);
        check(driver.getConfiguration() == config,
                "parameterless declarative driver did not receive host configuration");
        check(driver.initialize(), "declarative DLL driver failed to initialize");
        check(driver.getHeartBeat().getData().getHeartBeatStatus() == HeartBeatStatus.Normal.getValue(),
                "initialized DLL driver heartbeat was reported as disconnected");
        OffsetDateTime.parse(driver.getHeartBeat().getData().getHeartBeatTime());
        driver.close();
    }

    private static void testJavaDispatchCorrectness() {
        CorrectnessDriver driver = new CorrectnessDriver(DeviceConfiguration.createDll("correctness"));
        check(driver.initialize(), "correctness driver failed to initialize");

        FunctionData function = new FunctionData();
        function.setFunctionName("async_failure");
        Result<Finish> functionResult = driver.executeFunction(function);
        check(!functionResult.isSuccess() && "async failure".equals(functionResult.getMessage()),
                "CompletableFuture<Result<Finish>> failure was converted to success");

        SetData missing = new SetData();
        missing.setSetName("missing");
        missing.setSetValue("x");
        check(!driver.executeSet(Arrays.asList(missing)).isSuccess(),
                "unknown Set was silently accepted");

        SetData rejected = new SetData();
        rejected.setSetName("reject");
        rejected.setSetValue("x");
        check(!driver.executeSet(Arrays.asList(rejected)).isSuccess(),
                "boolean false Set was converted to success");

        FunctionData typed = new FunctionData();
        typed.setFunctionName("typed");
        typed.setFunctionParam(Collections.singletonMap("count", "7"));
        check(driver.executeFunction(typed).isSuccess() && driver.lastCount == 7,
                "declarative typed parameter conversion failed");
        driver.close();
    }

    private static void testContractValidation() {
        boolean duplicateRejected = false;
        try {
            new DuplicateDriver();
        } catch (IllegalStateException expected) {
            duplicateRejected = true;
        }
        check(duplicateRejected, "duplicate annotation names were not rejected at startup");

        boolean invalidJsonRejected = false;
        try {
            new InvalidFormDriver();
        } catch (IllegalStateException expected) {
            invalidJsonRejected = true;
        }
        check(invalidJsonRejected, "invalid formJson was not rejected at startup");
    }

    private static void testDeviceCallResources() throws Exception {
        DeviceConfiguration config = DeviceConfiguration.createDll("device-call-contract");
        config.setDeviceCallResources(1);
        DeviceCallDriver driver = new DeviceCallDriver(config);
        CountDownLatch started = new CountDownLatch(2);
        AtomicInteger active = new AtomicInteger();
        AtomicInteger maximum = new AtomicInteger();

        CompletableFuture<Void> first = driver.runDeviceCall(started, active, maximum);
        CompletableFuture<Void> second = driver.runDeviceCall(started, active, maximum);
        check(started.await(2, TimeUnit.SECONDS), "business calls did not start concurrently");
        CompletableFuture.allOf(first, second).get(3, TimeUnit.SECONDS);
        check(maximum.get() == 1, "deviceCallResources=1 did not serialize actual API calls");
        driver.close();
    }

    private static void testNamedConnections() throws Exception {
        CommunicationProviderRegistry.register(new TestCommunicationProvider());
        ConnectionConfiguration first=new ConnectionConfiguration();first.setName("api");first.setType("contract-test");first.setDefault(true);first.setMaxConcurrency(2);first.setResourceGroup("vendor-sdk");
        ConnectionConfiguration second=new ConnectionConfiguration();second.setName("events");second.setType("contract-test");second.setMaxConcurrency(2);second.setResourceGroup("vendor-sdk");
        ConnectionManager manager=new ConnectionManager(Arrays.asList(first,second));check(manager.connect(),"named connections failed to connect");
        AtomicInteger active=new AtomicInteger(),maximum=new AtomicInteger();List<CompletableFuture<Void>> calls=new ArrayList<>();
        for(int i=0;i<6;i++){final String name=i%2==0?"api":"events";calls.add(CompletableFuture.runAsync(()->manager.execute(name,c->{int current=active.incrementAndGet();maximum.accumulateAndGet(current,Math::max);try{Thread.sleep(25);}catch(InterruptedException e){Thread.currentThread().interrupt();}finally{active.decrementAndGet();}return true;})));}
        CompletableFuture.allOf(calls.toArray(new CompletableFuture[0])).get(3,TimeUnit.SECONDS);
        check(maximum.get()==2,"shared Java resourceGroup did not enforce one concurrency budget");
        ConnectionConfiguration serial=new ConnectionConfiguration();serial.setType("serial");serial.setMaxConcurrency(9);check(serial.getEffectiveMaxConcurrency()==1,"serial physical concurrency must be one");
        manager.close();
    }

    private static void testCancellationEventsAndDynamicNests() throws Exception {
        CancelDriver driver=new CancelDriver(DeviceConfiguration.createDll("cancel"));check(driver.initialize(),"cancel driver failed to initialize");
        CountDownLatch progress=new CountDownLatch(1);driver.addEventListener(e->{if("progress".equals(e.getType())&&"cancel-1".equals(e.getInstructionId()))progress.countDown();});
        FunctionData data=new FunctionData();data.setFunctionName("cancel");data.setInstructionId("cancel-1");
        CompletableFuture<Result<Finish>> running=driver.executeFunctionAsync(data);check(progress.await(2,TimeUnit.SECONDS),"progress event not published");
        check(driver.cancelInstruction("cancel-1"),"Java instruction was not cancellable");check(!running.get(2,TimeUnit.SECONDS).isSuccess(),"cancelled Java instruction succeeded");
        check(driver.getRegisterInfo().getData().getAdvancedInfo().getEquipmentNests().size()==1,"dynamic Java nest was not registered");driver.close();
    }

    private static void testFileWorkflow() throws Exception {
        Path root=Files.createTempDirectory("ciai-file-contract-");FileWorkflow workflow=new FileWorkflow(root.toString());
        workflow.writeAtomic("out/result.bin",new byte[]{7,8,9});check(Arrays.equals(Files.readAllBytes(workflow.resolve("out/result.bin")),new byte[]{7,8,9}),"Java file workflow atomic write mismatch");
        boolean rejected=false;try{workflow.resolve("../escape.bin");}catch(IllegalArgumentException expected){rejected=true;}check(rejected,"Java file workflow allowed path escape");
        Files.delete(workflow.resolve("out/result.bin"));Files.delete(workflow.resolve("out"));Files.delete(root);
    }

    private static void testTcpTransactions() throws Exception {
        try (ServerSocket server = new ServerSocket(0)) {
            CompletableFuture<Void> serverTask = CompletableFuture.runAsync(() -> {
                try (Socket socket = server.accept()) {
                    InputStream input = socket.getInputStream();
                    OutputStream output = socket.getOutputStream();
                    int first = input.read();
                    check(first >= 0, "server did not receive first command");

                    socket.setSoTimeout(80);
                    try {
                        input.read();
                        throw new AssertionError(
                                "second TCP command arrived before the first transaction completed");
                    } catch (SocketTimeoutException expected) {
                        // Expected: the SDK transaction lock still owns the channel.
                    }

                    output.write(first + 10);
                    output.flush();
                    socket.setSoTimeout(1000);
                    int second = input.read();
                    check(second >= 0, "server did not receive second command");
                    output.write(second + 10);
                    output.flush();
                } catch (Exception e) {
                    throw new RuntimeException(e);
                }
            });

            try (TcpCommunication tcp = new TcpCommunication(
                    "127.0.0.1", server.getLocalPort(), 1000, 1000, 1000)) {
                check(tcp.connect(), "TCP contract client failed to connect");
                CompletableFuture<byte[]> first = tcp.sendAndReceiveAsync(new byte[]{1});
                Thread.sleep(10);
                CompletableFuture<byte[]> second = tcp.sendAndReceiveAsync(new byte[]{2});
                check(Arrays.equals(first.get(2, TimeUnit.SECONDS), new byte[]{11}),
                        "first TCP response was paired incorrectly");
                check(Arrays.equals(second.get(2, TimeUnit.SECONDS), new byte[]{12}),
                        "second TCP response was paired incorrectly");
            }
            serverTask.get(2, TimeUnit.SECONDS);
        }
    }

    private static void testTcpFraming() throws Exception {
        try (ServerSocket server = new ServerSocket(0)) {
            CompletableFuture<Void> serverTask = CompletableFuture.runAsync(() -> {
                try (Socket socket = server.accept()) {
                    InputStream input = socket.getInputStream();
                    OutputStream output = socket.getOutputStream();
                    check(input.read() == 9, "framing server did not receive command");
                    output.write(new byte[]{1, 2});
                    output.flush();
                    Thread.sleep(30);
                    output.write(new byte[]{3, 4});
                    output.flush();
                    check(input.read() == 8, "framing server did not receive delimiter command");
                    output.write(new byte[]{5, 6, (byte) 0xFF});
                    output.flush();
                } catch (Exception e) {
                    throw new RuntimeException(e);
                }
            });
            try (TcpCommunication tcp = new TcpCommunication(
                    "127.0.0.1", server.getLocalPort(), 1000, 1000, 1000)) {
                check(tcp.connect(), "framing client failed to connect");
                check(Arrays.equals(tcp.sendAndReadExact(new byte[]{9}, 4),
                                new byte[]{1, 2, 3, 4}),
                        "split fixed-length TCP frame was not reassembled");
                check(Arrays.equals(tcp.sendAndReadUntil(new byte[]{8}, (byte) 0xFF, 16),
                                new byte[]{5, 6, (byte) 0xFF}),
                        "delimited TCP frame was not read atomically");
            }
            serverTask.get(2, TimeUnit.SECONDS);
        }
    }

    private static void testResultNullSerialization() throws Exception {
        String json = new ObjectMapper().writeValueAsString(Result.failed("expected"));
        check(json.contains("\"data\":null"),
                "Java Result null serialization differs from the C# wire contract");
    }

    private static void testHttpCommunication() throws Exception {
        AtomicInteger requests = new AtomicInteger();
        HttpServer server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        server.createContext("/", exchange -> {
            requests.incrementAndGet();
            byte[] response;
            if ("POST".equals(exchange.getRequestMethod())) {
                ByteArrayOutputStream body = new ByteArrayOutputStream();
                byte[] buffer = new byte[128];
                int count;
                while ((count = exchange.getRequestBody().read(buffer)) >= 0) {
                    body.write(buffer, 0, count);
                }
                response = body.toByteArray();
            } else {
                response = new byte[]{42};
            }
            exchange.sendResponseHeaders(200, response.length);
            exchange.getResponseBody().write(response);
            exchange.close();
        });
        server.start();
        try (HttpCommunication http = new HttpCommunication(
                "http://127.0.0.1:" + server.getAddress().getPort(), 1000)) {
            check(http.connect(), "HTTP client-ready connect failed");
            check(requests.get() == 0, "HTTP connect unexpectedly probed the device root endpoint");
            check(Arrays.equals(http.sendAndReceive(new byte[]{7, 8}), new byte[]{7, 8}),
                    "generic HTTP sendAndReceive did not POST and return response bytes");
            check(Arrays.equals(http.receive(), new byte[]{42}),
                    "generic HTTP receive did not GET response bytes");
        } finally {
            server.stop(0);
        }
    }

    private static void testServerGuards() throws Exception {
        int port;
        try (ServerSocket reservation = new ServerSocket(0)) {
            port = reservation.getLocalPort();
        }
        HttpsOptions options = HttpsOptions.createHttp(port);
        options.setHost("127.0.0.1");
        options.setEnableCallback(false);
        options.setMaxRequestBodyBytes(32);
        options.setShutdownTimeoutMs(1000);
        CorrectnessDriver driver = new CorrectnessDriver(DeviceConfiguration.createDll("server"));
        check(driver.initialize(), "server guard driver failed to initialize");
        try (DriverHttpServer server = new DriverHttpServer(options, driver)) {
            server.start();
            check(requestStatus(port, "/Info", "POST", null) == 405,
                    "known Java endpoint with wrong method did not return 405");
            check(requestStatus(port, "/Info/extra", "GET", null) == 404,
                    "Java prefix route accepted an unknown path");
            check(requestStatus(port, "/Operation", "POST",
                    "{\"operationName\":\"x\",\"padding\":\"123456789012345678901234567890\"}") == 413,
                    "Java server did not enforce request body limit");
        }
    }

    private static int requestStatus(int port, String path, String method, String body) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(
                "http://127.0.0.1:" + port + path).openConnection();
        connection.setRequestMethod(method);
        connection.setConnectTimeout(1000);
        connection.setReadTimeout(1000);
        if (body != null) {
            connection.setDoOutput(true);
            byte[] bytes = body.getBytes("UTF-8");
            connection.getOutputStream().write(bytes);
        }
        int status = connection.getResponseCode();
        connection.disconnect();
        return status;
    }

    private static void testServerBackpressure() throws Exception {
        int port;
        try (ServerSocket reservation = new ServerSocket(0)) {
            port = reservation.getLocalPort();
        }
        HttpsOptions options = HttpsOptions.createHttp(port, "127.0.0.1");
        options.setEnableCallback(false);
        options.setFunctionQueueCapacity(1);
        options.setShutdownTimeoutMs(1000);
        BackpressureDriver driver = new BackpressureDriver(DeviceConfiguration.createDll("backpressure"));
        check(driver.initialize(), "backpressure driver failed to initialize");
        try (DriverHttpServer server = new DriverHttpServer(options, driver)) {
            server.start();
            try {
                check(requestStatus(port, "/Function", "POST",
                                "{\"functionName\":\"block\",\"instructionId\":\"q-1\"}") == 200,
                        "first Java Function should be accepted");
                check(driver.started.await(2, TimeUnit.SECONDS),
                        "Java Function worker did not start");
                check(requestStatus(port, "/Function", "POST",
                                "{\"functionName\":\"block\",\"instructionId\":\"q-2\"}") == 200,
                        "second Java Function should enter the bounded queue");
                check(requestStatus(port, "/Function", "POST",
                                "{\"functionName\":\"block\",\"instructionId\":\"q-3\"}") == 429,
                        "full Java Function queue should return 429");
            } finally {
                driver.release.countDown();
            }
        }
    }

    private static void check(boolean condition, String message) {
        if (!condition) {
            throw new AssertionError(message);
        }
    }

    @DeviceDriver(name = "声明式测试设备", nameEN = "DeclarativeDriver",
            model = "TEST-1", manufacturer = "CIAI")
    public static final class DeclarativeDriver extends DeviceDriverBase {
    }

    @DeviceDriver(name = "设备调用测试", nameEN = "DeviceCallDriver",
            model = "TEST-2", manufacturer = "CIAI", parallelizability = 1)
    public static final class DeviceCallDriver extends DeviceDriverBase {
        public DeviceCallDriver(DeviceConfiguration config) {
            super(config);
        }

        CompletableFuture<Void> runDeviceCall(CountDownLatch started, AtomicInteger active,
                                              AtomicInteger maximum) {
            return CompletableFuture.runAsync(() -> {
                started.countDown();
                executeDeviceCall(() -> {
                    int current = active.incrementAndGet();
                    maximum.accumulateAndGet(current, Math::max);
                    try {
                        Thread.sleep(60);
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                        throw new RuntimeException(e);
                    } finally {
                        active.decrementAndGet();
                    }
                });
            });
        }
    }

    @DeviceDriver(name = "背压测试", functionalResources = 1)
    public static final class BackpressureDriver extends DeviceDriverBase {
        final CountDownLatch started = new CountDownLatch(1);
        final CountDownLatch release = new CountDownLatch(1);

        BackpressureDriver(DeviceConfiguration config) {
            super(config);
        }

        @DeviceFunction(name = "block")
        public Result<Finish> block(FunctionData data) {
            started.countDown();
            try {
                if (!release.await(2, TimeUnit.SECONDS)) {
                    return Result.failed("test release timed out");
                }
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                return Result.failed("test interrupted");
            }
            return Result.success(Finish.success());
        }
    }

    @DeviceDriver(name = "正确性测试", nameEN = "CorrectnessDriver",
            model = "TEST-3", manufacturer = "CIAI")
    public static final class CorrectnessDriver extends DeviceDriverBase {
        int lastCount;

        CorrectnessDriver(DeviceConfiguration config) {
            super(config);
        }

        @DeviceFunction(name = "async_failure")
        public CompletableFuture<Result<Finish>> asyncFailure(FunctionData data) {
            return CompletableFuture.completedFuture(Result.failed("async failure"));
        }

        @DeviceFunction(name = "typed", formJson = "{\"type\":\"object\"}")
        public Result<Finish> typed(FunctionData data) {
            TypedParam param = requireFunctionParam(data, TypedParam.class);
            lastCount = param.count;
            return Result.success(Finish.success());
        }

        @DeviceSet(name = "reject")
        public boolean reject(String value) {
            return false;
        }
    }

    public static final class TypedParam {
        public int count;
    }

    public static final class TestCommunicationProvider implements ICommunicationProvider {
        public Collection<String> getTypes(){return Collections.singletonList("contract-test");}
        public void validate(ConnectionConfiguration c){}
        public ICommunication create(ConnectionConfiguration c){return new TestCommunication();}
    }
    public static final class TestCommunication implements ICommunication {
        private boolean connected;public boolean isConnected(){return connected;}
        public CompletableFuture<Boolean> connectAsync(){return CompletableFuture.completedFuture(connect());}
        public CompletableFuture<Void> disconnectAsync(){disconnect();return CompletableFuture.completedFuture(null);}
        public CompletableFuture<Boolean> sendAsync(byte[] d){return CompletableFuture.completedFuture(true);}
        public CompletableFuture<byte[]> receiveAsync(){return CompletableFuture.completedFuture(new byte[0]);}
        public CompletableFuture<byte[]> sendAndReceiveAsync(byte[] d){return CompletableFuture.completedFuture(d);}
        public boolean connect(){connected=true;return true;}public void disconnect(){connected=false;}public boolean send(byte[] d){return true;}public byte[] receive(){return new byte[0];}public byte[] sendAndReceive(byte[] d){return d;}
    }

    @DeviceDriver(name="取消与动态位置测试",functionalResources=1)
    public static final class CancelDriver extends DeviceDriverBase {
        CancelDriver(DeviceConfiguration c){super(c);}
        @DeviceFunction(name="cancel") public Result<Finish> cancel(FunctionData data){while(true){getCurrentExecution().throwIfCancellationRequested();reportProgress(10,"working",null);try{Thread.sleep(20);}catch(InterruptedException e){Thread.currentThread().interrupt();return Result.failed("interrupted");}}}
        protected Collection<EquipmentNest> getDynamicEquipmentNests(){EquipmentNest nest=new EquipmentNest();nest.setNestName("Storage-1");nest.setNestAccessibility(0);nest.setNestIsDestination(1);return Collections.singletonList(nest);}
    }

    @DeviceDriver(name = "重复测试", nameEN = "DuplicateDriver",
            model = "TEST-4", manufacturer = "CIAI")
    public static final class DuplicateDriver extends DeviceDriverBase {
        @DeviceGet(name = "same")
        public String first() { return "1"; }
        @DeviceGet(name = "same")
        public String second() { return "2"; }
    }

    @DeviceDriver(name = "表单测试", nameEN = "InvalidFormDriver",
            model = "TEST-5", manufacturer = "CIAI")
    public static final class InvalidFormDriver extends DeviceDriverBase {
        @DeviceFunction(name = "bad", formJson = "{bad")
        public Finish bad(FunctionData data) { return Finish.success(); }
    }
}

using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CiaiControllerSDK.Attributes;
using CiaiControllerSDK.Callback;
using CiaiControllerSDK.Communication;
using CiaiControllerSDK.Config;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Models;
using CiaiControllerSDK.Services;
using CiaiControllerSDK.Interfaces;
using CiaiControllerSDK.WebServer;
using CiaiControllerSDK.LegacyAdapter;
using YamlDotNet.Serialization;

namespace CiaiControllerSDK.ContractTests;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--legacy-echo")
        {
            LegacyAdapterServer.Run(request => request.Reverse().ToArray());
            return;
        }
        await TestDriverContractAsync();
        await TestDeclarativeConfigurationAsync();
        TestYamlContract();
        TestMachineReadableContracts();
        TestConfigurationPreflight();
        await TestNamedConnectionsAsync();
        await TestLegacyProcessBridgeAsync();
        await TestFileWorkflowAsync();
        await TestCancellationAndEventsAsync();
        await TestTcpCommunicationContractAsync();
        await TestHttpContractAsync();
        await TestFunctionQueueBackpressureAsync();
        await TestHttpServerGuardsAsync();
        TestResultContract();

        Console.WriteLine("CiaiControllerSDK contract tests passed.");
    }

    private static async Task TestDriverContractAsync()
    {
        await using var driver = new ContractDriver(new DeviceConfiguration
        {
            DeviceId = "contract-driver",
            CommunicationType = CommunicationType.DLL
        });

        Assert(await driver.InitializeAsync(), "DLL driver should initialize without ICommunication");
        Assert(driver.GetHeartBeat().Data.HeartBeatStatus == (int)HeartBeatStatus.Normal,
            "initialized DLL driver heartbeat should be normal");

        var direct = await driver.ExecuteFunctionAsync(new FunctionData
        {
            FunctionName = "direct",
            InstructionId = "instruction-1",
            NestId = "nest-1"
        });
        Assert(direct.IsSuccess && direct.Data.Completion == "finish", "direct Result<Finish> function failed");

        var asyncResult = await driver.ExecuteFunctionAsync(new FunctionData { FunctionName = "async" });
        Assert(asyncResult.IsSuccess && asyncResult.Data.ResultOutput?.Count == 1,
            "Task<Result<Finish>> function failed");

        var valueTaskResult = await driver.ExecuteFunctionAsync(new FunctionData { FunctionName = "value-task" });
        Assert(valueTaskResult.IsSuccess && valueTaskResult.Data.Completion == "finish",
            "ValueTask<Result<Finish>> function failed");

        var slowFunction = driver.ExecuteFunctionAsync(new FunctionData { FunctionName = "slow" });
        var slowOperation = driver.ExecuteOperationAsync(new OperationData { OperationName = "slow-operation" });
        await Task.WhenAll(slowFunction, slowOperation);
        Assert(driver.MaxConcurrentEndpointCalls == 2,
            "business endpoints should remain independent from the device-call resource limit");
        Assert(driver.MaxConcurrentDeviceCalls == 1,
            "deviceCallResources=1 should serialize only the actual DLL/API calls");

        var operation = await driver.ExecuteOperationAsync(new OperationData { OperationName = "result-operation" });
        Assert(operation.IsSuccess && operation.Data == false,
            "Result<bool> operation should preserve a successful false value");

        var set = await driver.ExecuteSetAsync(new List<SetData>
        {
            new() { SetName = "mode", SetValue = "safe" }
        });
        Assert(set.IsSuccess && driver.Mode == "safe", "Result<bool> set failed");
        var rejectedSet = await driver.ExecuteSetAsync(new List<SetData>
        {
            new() { SetName = "reject", SetValue = "value" }
        });
        Assert(!rejectedSet.IsSuccess, "Result<bool>.Success(false) set should be treated as failure");
        var typedSet = await driver.ExecuteSetAsync(new List<SetData>
        {
            new() { SetName = "count", SetValue = "7" }
        });
        Assert(typedSet.IsSuccess && driver.Count == 7,
            "declarative Set value was not converted to the annotated parameter type");

        var enterExit = await driver.ExecuteEnterExitAsync(new EnterOrExitData { EnterOrExitName = "in" });
        Assert(enterExit.IsSuccess && enterExit.Data.Completion == "finish", "Result<Finish> enter/exit failed");

        var registration = driver.GetRegisterInfo();
        Assert(registration.IsSuccess, "register info failed");
        Assert(registration.Data.AdvancedInfo.EquipmentFunctions.Count == 5, "function registration mismatch");
        Assert(registration.Data.AdvancedInfo.EquipmentOperations.Count == 2, "operation registration mismatch");
        Assert(registration.Data.AdvancedInfo.EquipmentSetInfos.Count == 3, "set registration mismatch");
        Assert(registration.Data.AdvancedInfo.EquipmentGetInfos.Count == 1, "get registration mismatch");
        Assert(registration.Data.AdvancedInfo.EquipmentNests.Count == 2, "static/dynamic nest registration mismatch");
        Assert(registration.Data.AdvancedInfo.EquipmentEnterAndExit?.EnterAndExitName == "in",
            "enter/exit registration mismatch");

        Assert(await driver.AcquireForTestAsync(TimeSpan.FromMilliseconds(50)),
            "first functional resource acquisition should succeed");
        Assert(!await driver.AcquireForTestAsync(TimeSpan.FromMilliseconds(20)),
            "functional resource acquisition should time out when exhausted");
        driver.ReleaseForTest();
    }

    private static async Task TestDeclarativeConfigurationAsync()
    {
        var configuration = new DeviceConfiguration
        {
            DeviceId = "declarative-driver",
            CommunicationType = CommunicationType.DLL,
            DeviceCallResources = 2,
            DeviceCallTimeout = 4321
        };

        await using var driver = DriverHost.CreateDriver<DeclarativeDriver>(configuration);
        Assert(ReferenceEquals(driver.Configuration, configuration),
            "parameterless declarative driver should receive host configuration");
        Assert(driver.DeviceCallResources == 2,
            "declarative driver should receive deviceCallResources from configuration");
        Assert(await driver.InitializeAsync(), "declarative DLL driver initialization failed");
    }

    private static void TestYamlContract()
    {
        var defaults = new HttpsOptions();
        Assert(!defaults.UseHttps && !defaults.EnableCallback && defaults.Port == 8080 && defaults.Protocol == "TLSv1.2" &&
               defaults.EnabledProtocols.SequenceEqual(new[] { "TLSv1.2" }) && defaults.Ciphers.Length == 0,
            "portable HTTP/TLS defaults mismatch");

        var omittedServer = YamlConfigLoader.Parse("device:\n  communicationType: DLL\n").ToHttpsOptions();
        Assert(!omittedServer.UseHttps && omittedServer.Port == 8080,
            "omitted server section should use safe HTTP defaults");

        var sample = YamlConfigLoader.Load(Path.Combine(AppContext.BaseDirectory, "application.sample.yml"));
        Assert(!sample.ToHttpsOptions().UseHttps &&
               sample.ToDeviceConfiguration().CommunicationType == CommunicationType.Serial,
            "published C# sample configuration must parse and use safe defaults");
        Assert(ConfigurationValidator.Validate(sample.ToHttpsOptions(), sample.ToDeviceConfiguration())
                .All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error),
            "published C# sample configuration must pass hardware-free validation");

        var heartbeat = new HeartBeatInfo();
        using var heartbeatJson = JsonDocument.Parse(JsonSerializer.Serialize(heartbeat));
        Assert(DateTimeOffset.TryParse(
                   heartbeatJson.RootElement.GetProperty("heartBeatTime").GetString(), out _),
            "heartbeat time must contain a parseable UTC offset");

        const string commentOnlyEnvironmentYaml = @"# Optional HTTPS password: ${CIAI_SDK_COMMENT_ENV_MUST_NOT_BE_READ}
server:
  port: 8080
  host: localhost
  useHttps: false
device:
  communicationType: DLL
";
        Assert(YamlConfigLoader.Parse(commentOnlyEnvironmentYaml).Server.Port == 8080,
            "environment placeholders in YAML comments must not be expanded");

        const string invalidClientAuthYaml = @"server:
  useHttps: false
  clientAuth:
    enabled: true
    mode: typo
device:
  communicationType: DLL
";
        AssertThrows<ArgumentException>(
            () => YamlConfigLoader.Parse(invalidClientAuthYaml).ToHttpsOptions(),
            "unknown client authentication mode should be rejected");

        const string dllYaml = @"server:
  port: 8080
  host: ""127.0.0.1""
  useHttps: false
  maxConcurrentRequests: 12
  maxRequestBodyBytes: 4096
  functionQueueCapacity: 7
  idempotencyCapacity: 20
  shutdownTimeoutMs: 2000
callback:
  url: ""http://localhost/callback""
  timeoutMs: 1234
  enabled: false
device:
  deviceId: ""dll-device""
  communicationType: ""DLL""
  deviceCallResources: 3
  deviceCallTimeoutMs: 4567
  simulated: true
  settings:
    retryCount: 3
    secretAlias: ""${CIAI_SDK_TEST_ENV_THAT_SHOULD_NOT_EXIST:-fallback-secret}""
";

        var config = YamlConfigLoader.Parse(dllYaml);
        var options = config.ToHttpsOptions();
        var device = config.ToDeviceConfiguration();
        Assert(options.Port == 8080 && !options.UseHttps && !options.EnableCallback,
            "server/callback YAML mapping mismatch");
        Assert(options.MaxConcurrentRequests == 12 && options.MaxRequestBodyBytes == 4096 &&
               options.FunctionQueueCapacity == 7 && options.IdempotencyCapacity == 20 &&
               options.ShutdownTimeoutMs == 2000,
            "server production limits were not mapped");
        Assert(device.CommunicationType == CommunicationType.DLL, "DLL communication type mapping mismatch");
        Assert(device.DeviceCallResources == 3 && device.DeviceCallTimeout == 4567,
            "DLL/API device call resource mapping mismatch");
        Assert(device.GetExtraSetting("simulated", false), "direct custom YAML field was not captured");
        Assert(device.GetExtraSetting("retryCount", 0) == 3, "device.settings YAML field was not captured");
        Assert(device.GetExtraSetting("secretAlias", "") == "fallback-secret",
            "environment variable fallback was not expanded");

        const string namedYaml = @"device:
  deviceId: named
  connections:
    control:
      type: tcp
      default: true
      host: 127.0.0.1
      port: 5000
      maxConcurrency: 9
    serialBus:
      type: serial
      serialPort: COM9
      flowControl: rtscts
      dtrEnable: true
";
        var named = YamlConfigLoader.Parse(namedYaml).ToDeviceConfiguration();
        Assert(named.Connections.Count == 2 && named.Connections["control"].IsDefault &&
               named.Connections["serialBus"].FlowControl == "rtscts",
            "named connections YAML mapping mismatch");
        Assert(named.Connections["control"].EffectiveMaxConcurrency == 1,
            "TCP YAML maxConcurrency must be forced to one");

        const string disabledClientAuthYaml = @"server:
  port: 8082
  host: ""localhost""
  useHttps: false
  clientAuth:
    enabled: false
    mode: ""need""
device:
  communicationType: ""DLL""
";
        var disabledClientAuth = YamlConfigLoader.Parse(disabledClientAuthYaml).ToHttpsOptions();
        Assert(disabledClientAuth.ClientAuth == ClientAuthMode.None &&
               !disabledClientAuth.RequireClientCertificate,
            "clientAuth.enabled=false should disable client certificate authentication");

        const string serialYaml = @"server:
  port: 8081
  host: ""localhost""
  useHttps: false
device:
  communicationType: ""Serial""
  serial:
    port: ""COM9""
    baudRate: 115200
    dataBits: 7
    stopBits: 2
    parity: ""even""
    timeoutMs: 2500
    readTimeoutMs: 2600
    writeTimeoutMs: 2700
    encoding: ""ascii""
";

        var serial = YamlConfigLoader.Parse(serialYaml).ToDeviceConfiguration();
        Assert(serial.CommunicationType == CommunicationType.Serial && serial.SerialPort == "COM9",
            "serial communication mapping mismatch");
        Assert(serial.BaudRate == 115200 && serial.DataBits == 7 && serial.StopBits == 2 &&
               serial.Parity == "even" && serial.ReadTimeout == 2600 &&
               serial.WriteTimeout == 2700 && serial.Encoding == "ascii",
            "serial detail mapping mismatch");

        using var serialCommunication = CommunicationFactory.Create(serial) as SerialCommunication;
        Assert(serialCommunication != null, "communication factory should create SerialCommunication");

        const string unknownCommunicationYaml = @"server:
  port: 8083
  host: ""localhost""
  useHttps: false
device:
  communicationType: ""typo""
";
        AssertThrows<ArgumentException>(
            () => YamlConfigLoader.Parse(unknownCommunicationYaml).ToDeviceConfiguration(),
            "unknown communication type should not silently fall back to TCP");
    }

    private static void TestConfigurationPreflight()
    {
        var invalidSerial = DeviceConfiguration.CreateSerial("serial", "COM1", baudRate: 0,
            dataBits: 9, stopBits: 3, parity: "invalid", encoding: "invalid");
        var serialDiagnostics = ConfigurationValidator.Validate(invalidSerial);
        Assert(serialDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error &&
                                          d.Path == "device.serial"),
            "legacy serial settings must be validated before opening the port");

        var invalidNamed = DeviceConfiguration.CreateDll("named-invalid");
        invalidNamed.Connections["api"] = new ConnectionConfiguration
        {
            Name = "api",
            Type = "http",
            BaseUrl = "ftp://device",
            MaxConcurrency = 0
        };
        Assert(ConfigurationValidator.Validate(invalidNamed)
                .Any(d => d.Severity == DiagnosticSeverity.Error &&
                          d.Path == "device.connections.api"),
            "named connection URL and resource settings must be validated");

        var tempPath = Path.Combine(Path.GetTempPath(), $"ciai-preflight-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(tempPath, "server:\n  useHttps: false\ndevice:\n  communicationType: DLL\n");
            var report = DriverHost.ValidateConfiguration(tempPath);
            Assert(report.IsValid && report.HasWarnings && report.ConfigPath == Path.GetFullPath(tempPath),
                "device-free configuration preflight should return warnings without starting hardware");

            File.WriteAllText(tempPath, "server:\n  useHttps: false\n");
            report = DriverHost.ValidateConfiguration(tempPath);
            Assert(!report.IsValid && report.Diagnostics.Any(d => d.Path == "device"),
                "configuration preflight must reject a missing device section");

            var adapterName = Path.GetFileNameWithoutExtension(tempPath) + "-adapter";
            var adapterDirectory = Path.Combine(Path.GetDirectoryName(tempPath)!, adapterName);
            Directory.CreateDirectory(adapterDirectory);
            File.WriteAllBytes(Path.Combine(adapterDirectory, "adapter.exe"), Array.Empty<byte>());
            File.WriteAllText(tempPath, "server:\n  useHttps: false\ndevice:\n  deviceId: process\n" +
                                        "  connections:\n    vendor:\n      type: process\n      default: true\n" +
                                        $"      workingDirectory: ./{adapterName}\n      executable: adapter.exe\n");
            report = DriverHost.ValidateConfiguration(tempPath);
            Assert(report.IsValid,
                "process paths relative to application.yml should resolve without changing working directory");
            File.Delete(Path.Combine(adapterDirectory, "adapter.exe"));
            Directory.Delete(adapterDirectory);

            File.WriteAllText(tempPath, "server:\n  useHttps: false\ndevice:\n  communicationType: DLL\n" +
                                        "  settings:\n    vendorFile: ./vendor/options.json\n");
            var deviceConfig = YamlConfigLoader.Load(tempPath).ToDeviceConfiguration();
            Assert(deviceConfig.ResolvePath(deviceConfig.GetExtraSetting<string>("vendorFile")) ==
                   Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tempPath)!, "vendor/options.json")),
                "vendor setting paths should resolve relative to application.yml");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TestMachineReadableContracts()
    {
        var specDirectory = Path.Combine(AppContext.BaseDirectory, "spec");
        var openApiText = File.ReadAllText(Path.Combine(specDirectory,
            "ciai-driver-api.openapi.yaml"));
        var openApi = new DeserializerBuilder().Build()
            .Deserialize<Dictionary<object, object>>(openApiText);
        var paths = (Dictionary<object, object>)openApi["paths"];
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "/Info", "/HeartBeat", "/Function", "/Operation", "/Set", "/Get", "/EnterAndExit"
        };
        Assert(paths.Keys.Select(key => key.ToString()).ToHashSet(StringComparer.Ordinal)
                .SetEquals(expected),
            "OpenAPI must contain exactly the seven implemented endpoints");

        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(specDirectory,
            "application.schema.json")));
        Assert(schema.RootElement.GetProperty("$schema").GetString()!
                .Contains("2020-12", StringComparison.Ordinal),
            "configuration schema must use JSON Schema 2020-12");
        Assert(schema.RootElement.GetProperty("required")[0].GetString() == "device",
            "configuration schema must require the device section");
    }

    private static async Task TestNamedConnectionsAsync()
    {
        CommunicationProviderRegistry.Register(new TestCommunicationProvider());
        var configuration = new DeviceConfiguration();
        configuration.Connections["api"] = new ConnectionConfiguration
        {
            Name = "api", Type = "contract-test", IsDefault = true,
            MaxConcurrency = 2, ResourceGroup = "vendor-sdk"
        };
        configuration.Connections["events"] = new ConnectionConfiguration
        {
            Name = "events", Type = "contract-test", MaxConcurrency = 2,
            ResourceGroup = "vendor-sdk"
        };
        Assert(ConfigurationValidator.Validate(configuration).All(d => d.Severity != DiagnosticSeverity.Error),
            "valid named connections produced diagnostics errors");
        await using var manager = new ConnectionManager(configuration.Connections.Values);
        Assert(await manager.ConnectAsync(), "named connections failed to connect");
        var active = 0; var maximum = 0;
        var calls = Enumerable.Range(0, 6).Select(i => manager.ExecuteAsync(
            i % 2 == 0 ? "api" : "events", async _ =>
            {
                var current = Interlocked.Increment(ref active);
                while (true)
                {
                    var old = Volatile.Read(ref maximum);
                    if (current <= old || Interlocked.CompareExchange(ref maximum, current, old) == old) break;
                }
                await Task.Delay(25);
                Interlocked.Decrement(ref active);
                return true;
            }));
        await Task.WhenAll(calls);
        Assert(maximum == 2, "shared resourceGroup did not enforce one concurrency budget");
        Assert(new ConnectionConfiguration { Type = "serial", MaxConcurrency = 9 }.EffectiveMaxConcurrency == 1,
            "serial physical concurrency must always be one");
    }

    private static async Task TestCancellationAndEventsAsync()
    {
        await using var driver = new ContractDriver(new DeviceConfiguration { CommunicationType = CommunicationType.DLL });
        Assert(await driver.InitializeAsync(), "cancellation driver initialization failed");
        var progressSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        driver.EventPublished += (_, e) => { if (e.Type == "progress" && e.InstructionId == "cancel-1") progressSeen.TrySetResult(); };
        var running = driver.ExecuteFunctionAsync(new FunctionData { FunctionName = "cancel", InstructionId = "cancel-1" });
        await progressSeen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(driver.CancelInstruction("cancel-1"), "running instruction was not cancellable");
        Assert(!(await running).IsSuccess, "cancelled instruction unexpectedly succeeded");
    }

    private static async Task TestLegacyProcessBridgeAsync()
    {
        var executable = Environment.ProcessPath;
        Assert(!string.IsNullOrWhiteSpace(executable), "current executable path unavailable");
        var configuration = new ConnectionConfiguration
        {
            Name = "vendor", Type = "process", Executable = executable,
            Arguments = new List<string> { "--legacy-echo" }, ShutdownTimeoutMs = 1000
        };
        using var communication = new ProcessCommunication(configuration);
        Assert(await communication.ConnectAsync(), "legacy adapter process failed to start");
        var response = await communication.SendAndReceiveAsync(new byte[] { 1, 2, 3, 4 });
        Assert(response != null && response.SequenceEqual(new byte[] { 4, 3, 2, 1 }),
            "legacy adapter length-prefixed protocol mismatch");
    }

    private static async Task TestFileWorkflowAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ciai-file-contract-" + Guid.NewGuid().ToString("N"));
        var workflow = new FileWorkflow(root);
        await workflow.WriteAtomicAsync("out/result.bin", new byte[] { 7, 8, 9 });
        Assert(File.ReadAllBytes(workflow.Resolve("out/result.bin")).SequenceEqual(new byte[] { 7, 8, 9 }),
            "file workflow atomic write mismatch");
        AssertThrows<InvalidOperationException>(() => workflow.Resolve("../escape.bin"),
            "file workflow allowed a path outside its root");
        File.Delete(workflow.Resolve("out/result.bin"));
        Directory.Delete(workflow.Resolve("out"));
        Directory.Delete(root);
    }

    private static async Task TestTcpCommunicationContractAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            await using var stream = accepted.GetStream();
            var firstCommand = new byte[1];
            Assert(await stream.ReadAsync(firstCommand) == 1, "server did not receive first TCP command");

            var secondCommand = new byte[1];
            var secondRead = stream.ReadAsync(secondCommand).AsTask();
            var earlyRead = await Task.WhenAny(secondRead, Task.Delay(80));
            Assert(earlyRead != secondRead,
                "a second TCP request entered before the first request-response transaction completed");

            await stream.WriteAsync(new[] { (byte)(firstCommand[0] + 10) });
            Assert(await secondRead == 1, "server did not receive second TCP command");
            await stream.WriteAsync(new[] { (byte)(secondCommand[0] + 10) });
            await Task.Delay(30);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("A|B|"));
        });

        using var communication = new TcpCommunication("127.0.0.1", port, 1000, 1000, 1000);
        Assert(await communication.ConnectAsync(), "TCP communication failed to connect");

        var first = communication.SendAndReadExactAsync(new byte[] { 1 }, 1);
        await Task.Delay(10);
        var second = communication.SendAndReadExactAsync(new byte[] { 2 }, 1);
        await Task.WhenAll(first, second);
        Assert(first.Result.SequenceEqual(new byte[] { 11 }) && second.Result.SequenceEqual(new byte[] { 12 }),
            "TCP request-response pairing mismatch");

        var frameA = await communication.ReadUntilAsync((byte)'|');
        var frameB = await communication.ReadUntilAsync((byte)'|');
        Assert(Encoding.ASCII.GetString(frameA) == "A|" && Encoding.ASCII.GetString(frameB) == "B|",
            "TCP frame reader should retain additional frames from the same network packet");

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        using var autoListener = new TcpListener(IPAddress.Loopback, 0);
        autoListener.Start();
        var autoPort = ((IPEndPoint)autoListener.LocalEndpoint).Port;
        var acceptTask = autoListener.AcceptTcpClientAsync();
        await using var autoDriver = new ContractDriver(DeviceConfiguration.CreateTcp(
            "auto-tcp", "127.0.0.1", autoPort, 1000));
        Assert(await autoDriver.InitializeAsync(),
            "base InitializeAsync should create and connect configured TCP communication");
        using var autoAccepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(autoDriver.IsConnected, "automatically configured TCP driver should be connected");
    }

    private static async Task TestHttpContractAsync()
    {
        using var disabledCallback = HttpCallback.CreateDisabled();
        Assert(await disabledCallback.PostRawAsync("{}"), "disabled callback should be a successful no-op");

        await using var driver = new ContractDriver(new DeviceConfiguration { CommunicationType = CommunicationType.DLL });
        await driver.InitializeAsync();
        using var handler = new RequestHandler(driver, "http://localhost/callback", 100, enableCallback: false);

        var accepted = await handler.HandleRequestAsync(
            RouteBuilder.Endpoints.Function,
            "POST",
            "{\"functionName\":\"direct\",\"instructionId\":\"i-1\",\"nestId\":\"n-1\"}");
        Assert(accepted.StatusCode == 200, "Function endpoint should accept valid requests");
        using (var document = JsonDocument.Parse(accepted.Body))
        {
            Assert(document.RootElement.GetProperty("code").GetString() == CommonCode.Success,
                "Function accept response code mismatch");
        }
        var duplicate = await handler.HandleRequestAsync(
            RouteBuilder.Endpoints.Function,
            "POST",
            "{\"functionName\":\"direct\",\"instructionId\":\"i-1\",\"nestId\":\"n-1\"}");
        Assert(duplicate.StatusCode == 200 && duplicate.Body.Contains("already accepted"),
            "duplicate instructionId should be idempotently acknowledged");
        for (var i = 0; i < 50 && driver.DirectCalls == 0; i++)
            await Task.Delay(10);
        Assert(driver.DirectCalls == 1, "duplicate Function request was executed more than once");

        var methodNotAllowed = await handler.HandleRequestAsync(RouteBuilder.Endpoints.Info, "POST");
        Assert(methodNotAllowed.StatusCode == 405, "known endpoint with wrong method should return 405");

        var options = HttpsOptions.CreateHttp(8080, "127.0.0.1");
        Assert(options.GetListenPrefix() == "http://127.0.0.1:8080/", "configured host was ignored");
    }

    private static void TestResultContract()
    {
        Assert(Result<object>.Unauthorized().Code == CommonCode.Unauthorized, "unauthorized result mismatch");
        Assert(Result<object>.Timeout().Code == CommonCode.Timeout, "timeout result mismatch");
        Assert(Result<object>.ServerError().Code == CommonCode.ServerError, "server error result mismatch");
        Assert(Result<object>.ParametersMissing().Code == CommonCode.ParametersMissing,
            "parameters missing result mismatch");
    }

    private static async Task TestFunctionQueueBackpressureAsync()
    {
        await using var driver = new BackpressureDriver(
            new DeviceConfiguration { CommunicationType = CommunicationType.DLL });
        Assert(await driver.InitializeAsync(), "backpressure driver initialization failed");
        using var handler = new RequestHandler(
            driver, enableCallback: false, functionQueueCapacity: 1, shutdownTimeoutMs: 1000);

        try
        {
            Assert((await handler.HandleRequestAsync(RouteBuilder.Endpoints.Function, "POST",
                "{\"functionName\":\"block\",\"instructionId\":\"q-1\"}")).StatusCode == 200,
                "first Function should be accepted");
            await driver.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert((await handler.HandleRequestAsync(RouteBuilder.Endpoints.Function, "POST",
                "{\"functionName\":\"block\",\"instructionId\":\"q-2\"}")).StatusCode == 200,
                "second Function should enter the bounded queue");
            Assert((await handler.HandleRequestAsync(RouteBuilder.Endpoints.Function, "POST",
                "{\"functionName\":\"block\",\"instructionId\":\"q-3\"}")).StatusCode == 429,
                "full Function queue should return 429");
        }
        finally
        {
            driver.Release();
        }
    }

    private static async Task TestHttpServerGuardsAsync()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        await using var driver = new ContractDriver(
            new DeviceConfiguration { CommunicationType = CommunicationType.DLL });
        Assert(await driver.InitializeAsync(), "server guard driver initialization failed");
        await using var handler = new RequestHandler(
            driver, enableCallback: false, shutdownTimeoutMs: 1000);
        var options = HttpsOptions.CreateHttp(port, "localhost");
        options.MaxRequestBodyBytes = 32;
        options.ShutdownTimeoutMs = 1000;
        await using var server = new HttpsServer(options, handler);
        await server.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        using var wrongMethod = await client.PostAsync("/Info", new StringContent(""));
        Assert((int)wrongMethod.StatusCode == 405,
            "known .NET endpoint with wrong method did not return 405");
        using var unknown = await client.GetAsync("/Info/extra");
        Assert((int)unknown.StatusCode == 404, ".NET server accepted an unknown path");
        using var tooLarge = await client.PostAsync("/Operation",
            new StringContent(new string('x', 64), Encoding.UTF8, "application/json"));
        Assert((int)tooLarge.StatusCode == 413, ".NET server did not enforce request body limit");

        await server.StopAsync();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}

[DeviceDriver(
    "契约测试设备",
    NameEN = "ContractDriver",
    Model = "TEST-1",
    Manufacturer = "CIAI",
    Author = "SDK",
    EquipmentClass = "Test",
    EquipmentType = 1,
    FunctionalResources = 1,
    Parallelizability = 1)]
internal sealed class ContractDriver : DeviceDriverBase
{
    private readonly TaskCompletionSource<bool> _concurrentEndpointsStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _startedEndpointCount;
    private int _activeEndpointCalls;
    private int _activeDeviceCalls;
    private int _maxConcurrentEndpointCalls;
    private int _maxConcurrentDeviceCalls;
    private int _directCalls;

    public string Mode { get; private set; }
    public int MaxConcurrentEndpointCalls => _maxConcurrentEndpointCalls;
    public int MaxConcurrentDeviceCalls => _maxConcurrentDeviceCalls;
    public int DirectCalls => _directCalls;
    public int Count { get; private set; }

    public ContractDriver(DeviceConfiguration configuration) : base(configuration)
    {
    }

    [DeviceFunction("direct", TitleCN = "直接功能", TitleEN = "Direct")]
    public Result<Finish> Direct(FunctionData data)
    {
        Interlocked.Increment(ref _directCalls);
        return Result<Finish>.Success(Finish.Success());
    }

    [DeviceFunction("async", TitleCN = "异步功能", TitleEN = "Async")]
    public Task<Result<Finish>> Async(FunctionData data)
    {
        return Task.FromResult(Result<Finish>.Success(new Finish
        {
            Completion = "finish",
            ResultOutput = new List<ResultOutput> { new() { Name = "value", ResultData = 1 } }
        }));
    }

    [DeviceFunction("value-task", TitleCN = "值任务功能", TitleEN = "Value task")]
    public ValueTask<Result<Finish>> ValueTaskFunction(FunctionData data)
    {
        return ValueTask.FromResult(Result<Finish>.Success(Finish.Success()));
    }

    [DeviceFunction("slow", TitleCN = "并行业务功能", TitleEN = "Concurrent business function")]
    public async Task<Result<Finish>> Slow(FunctionData data)
    {
        await RunConcurrentEndpointAsync();
        return Result<Finish>.Success(Finish.Success());
    }

    [DeviceFunction("cancel", TitleCN = "取消测试", TitleEN = "Cancellation")]
    public async Task<Result<Finish>> Cancel(FunctionData data)
    {
        while (true)
        {
            ExecutionCancellationToken.ThrowIfCancellationRequested();
            ReportProgress(10, "working");
            await Task.Delay(20, ExecutionCancellationToken);
        }
    }

    [DeviceOperation("result-operation", TitleCN = "结果操作", TitleEN = "Result operation")]
    public Result<bool> ResultOperation(OperationData data)
    {
        return Result<bool>.Success(false);
    }

    [DeviceOperation("slow-operation", TitleCN = "并行业务操作", TitleEN = "Concurrent business operation")]
    public async Task<Result<bool>> SlowOperation(OperationData data)
    {
        await RunConcurrentEndpointAsync();
        return Result<bool>.Success(true);
    }

    [DeviceSet("mode", TitleCN = "模式", TitleEN = "Mode")]
    public Result<bool> SetMode(object value)
    {
        Mode = value?.ToString();
        return Result<bool>.Success(true);
    }

    [DeviceSet("reject", TitleCN = "拒绝值", TitleEN = "Reject value")]
    public Result<bool> Reject(object value) => Result<bool>.Success(false);

    [DeviceSet("count", TitleCN = "数量", TitleEN = "Count", Type = "number")]
    public bool SetCount(int value)
    {
        Count = value;
        return true;
    }

    [DeviceGet("mode", TitleCN = "模式", TitleEN = "Mode")]
    public string GetMode() => Mode ?? string.Empty;

    [DeviceEnterExit("in", TitleCN = "入板", TitleEN = "Plate in")]
    public Result<Finish> PlateIn(EnterOrExitData data) => Result<Finish>.Success(Finish.Success());

    public Task<bool> AcquireForTestAsync(TimeSpan timeout) => AcquireResourceAsync(timeout);

    public void ReleaseForTest() => ReleaseResource();

    private async Task RunConcurrentEndpointAsync()
    {
        var endpointCount = Interlocked.Increment(ref _activeEndpointCalls);
        UpdateMaximum(ref _maxConcurrentEndpointCalls, endpointCount);
        if (Interlocked.Increment(ref _startedEndpointCount) == 2)
            _concurrentEndpointsStarted.TrySetResult(true);

        try
        {
            await _concurrentEndpointsStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await ExecuteDeviceCallAsync(async () =>
            {
                var deviceCount = Interlocked.Increment(ref _activeDeviceCalls);
                UpdateMaximum(ref _maxConcurrentDeviceCalls, deviceCount);
                try
                {
                    await Task.Delay(60);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeDeviceCalls);
                }
            });
        }
        finally
        {
            Interlocked.Decrement(ref _activeEndpointCalls);
        }
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (candidate <= current || Interlocked.CompareExchange(ref maximum, candidate, current) == current)
                return;
        }
    }

    [DeviceNest(Order = 0)]
    public EquipmentNest Nest => new() { NestName = "Nest-1", NestAccessibility = 1 };

    protected override IEnumerable<EquipmentNest> GetDynamicEquipmentNests()
    {
        yield return new EquipmentNest { NestName = "Storage-1", NestAccessibility = 0, NestIsDestination = 1 };
    }
}

internal sealed class TestCommunicationProvider : ICommunicationProvider
{
    public IEnumerable<string> Types => new[] { "contract-test" };
    public void Validate(ConnectionConfiguration configuration) { }
    public ICommunication Create(ConnectionConfiguration configuration) => new TestCommunication();
}

internal sealed class TestCommunication : ICommunication
{
    public bool IsConnected { get; private set; }
    public Task<bool> ConnectAsync() { IsConnected = true; return Task.FromResult(true); }
    public Task DisconnectAsync() { IsConnected = false; return Task.CompletedTask; }
    public Task<bool> SendAsync(byte[] data) => Task.FromResult(true);
    public Task<byte[]> ReceiveAsync() => Task.FromResult(Array.Empty<byte>());
    public Task<byte[]> SendAndReceiveAsync(byte[] data) => Task.FromResult(data);
    public bool Connect() { IsConnected = true; return true; }
    public void Disconnect() { IsConnected = false; }
    public bool Send(byte[] data) => true;
    public byte[] Receive() => Array.Empty<byte>();
    public byte[] SendAndReceive(byte[] data) => data;
}

[DeviceDriver("声明式测试设备", FunctionalResources = 1)]
public sealed class DeclarativeDriver : DeviceDriverBase
{
    [DeviceFunction("ping", TitleCN = "测试", TitleEN = "Ping")]
    public Result<Finish> Ping(FunctionData data) => Result<Finish>.Success(Finish.Success());
}

[DeviceDriver("背压测试设备", FunctionalResources = 1)]
public sealed class BackpressureDriver : DeviceDriverBase
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BackpressureDriver(DeviceConfiguration configuration) : base(configuration) { }

    [DeviceFunction("block")]
    public async Task<Result<Finish>> Block(FunctionData data)
    {
        Started.TrySetResult();
        await _release.Task;
        return Result<Finish>.Success(Finish.Success());
    }

    public void Release() => _release.TrySetResult();
}

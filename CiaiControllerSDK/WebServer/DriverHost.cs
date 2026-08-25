using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Attributes;
using CiaiControllerSDK.Config;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Logging;

namespace CiaiControllerSDK.WebServer
{
    /// <summary>
    /// 驱动宿主 - 一行代码启动HTTP/HTTPS WebServer
    /// </summary>
    public static class DriverHost
    {
        /// <summary>
        /// 从YAML配置文件启动
        /// </summary>
        /// <typeparam name="TDriver">驱动类型</typeparam>
        /// <param name="configPath">配置文件路径，默认application.yml</param>
        /// <param name="loggerFactory">日志工厂（可选）</param>
        public static void Run<TDriver>(string configPath = "application.yml", ILoggerFactory loggerFactory = null)
            where TDriver : DeviceDriverBase
        {
            RunAsync<TDriver>(configPath, default, loggerFactory).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 从一个YAML文件加载服务器与设备配置并异步启动。
        /// </summary>
        public static Task RunAsync<TDriver>(string configPath = "application.yml",
            CancellationToken cancellationToken = default, ILoggerFactory loggerFactory = null)
            where TDriver : DeviceDriverBase
        {
            var config = YamlConfigLoader.Load(ResolveConfigPath(configPath));
            if (config.Device == null)
                throw new ConfigurationValidationException(new[]
                {
                    new ConfigurationDiagnostic
                    {
                        Severity = DiagnosticSeverity.Error,
                        Path = "device",
                        Message = "必须配置device节点"
                    }
                });
            return RunAsync<TDriver>(config.ToHttpsOptions(), config.ToDeviceConfiguration(),
                cancellationToken, loggerFactory);
        }

        /// <summary>加载并检查配置，不创建驱动、不连接设备、不启动端口。</summary>
        public static ConfigurationValidationReport ValidateConfiguration(
            string configPath = "application.yml")
        {
            var resolvedPath = ResolveConfigPath(configPath);
            try
            {
                var config = YamlConfigLoader.Load(resolvedPath);
                if (config.Device == null)
                {
                    return new ConfigurationValidationReport(resolvedPath, new[]
                    {
                        new ConfigurationDiagnostic
                        {
                            Severity = DiagnosticSeverity.Error,
                            Path = "device",
                            Message = "必须配置device节点"
                        }
                    });
                }

                return new ConfigurationValidationReport(resolvedPath,
                    ConfigurationValidator.Validate(config.ToHttpsOptions(),
                        config.ToDeviceConfiguration()));
            }
            catch (Exception ex) when (ex is not ConfigurationValidationException)
            {
                return new ConfigurationValidationReport(resolvedPath, new[]
                {
                    new ConfigurationDiagnostic
                    {
                        Severity = DiagnosticSeverity.Error,
                        Path = "configuration",
                        Message = ex.Message
                    }
                });
            }
        }

        /// <summary>
        /// 解析配置路径：优先采用调用者工作目录，其次采用可执行文件所在目录。
        /// </summary>
        public static string ResolveConfigPath(string configPath = "application.yml")
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("配置文件路径不能为空", nameof(configPath));

            if (Path.IsPathRooted(configPath))
                return Path.GetFullPath(configPath);

            var workingDirectoryCandidate = Path.GetFullPath(configPath);
            if (File.Exists(workingDirectoryCandidate))
                return workingDirectoryCandidate;

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configPath));
        }

        /// <summary>
        /// 从HttpsOptions启动
        /// </summary>
        /// <typeparam name="TDriver">驱动类型</typeparam>
        /// <param name="options">服务器配置选项</param>
        /// <param name="deviceConfig">设备配置（可选）</param>
        /// <param name="loggerFactory">日志工厂（可选）</param>
        public static void Run<TDriver>(HttpsOptions options, DeviceConfiguration deviceConfig = null, ILoggerFactory loggerFactory = null)
            where TDriver : DeviceDriverBase
        {
            RunAsync<TDriver>(options, deviceConfig, default, loggerFactory).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步启动
        /// </summary>
        /// <typeparam name="TDriver">驱动类型</typeparam>
        /// <param name="options">服务器配置选项</param>
        /// <param name="deviceConfig">设备配置（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="loggerFactory">日志工厂（可选），传入后将使用此工厂创建日志器</param>
        public static async Task RunAsync<TDriver>(
            HttpsOptions options,
            DeviceConfiguration deviceConfig = null,
            CancellationToken cancellationToken = default,
            ILoggerFactory loggerFactory = null)
            where TDriver : DeviceDriverBase
        {
            // 在创建驱动、通信和请求处理器之前应用日志工厂，保证整个SDK使用同一日志后端。
            if (loggerFactory != null)
            {
                LoggerProvider.SetLoggerFactory(loggerFactory);
            }

            // 使用传入的日志工厂或默认工厂
            var factory = loggerFactory ?? LoggerProvider.Factory;
            var logger = factory.CreateLogger(nameof(DriverHost));

            var config = deviceConfig ?? new DeviceConfiguration();
            ConfigurationValidator.ValidateAndThrow(options, config);

            // 验证驱动类型
            var driverType = typeof(TDriver);
            var driverAttr = driverType.GetCustomAttribute<DeviceDriverAttribute>();
            if (driverAttr == null)
            {
                throw new InvalidOperationException($"类型 {driverType.Name} 必须标记 [DeviceDriver] 属性");
            }

            var scheme = options.UseHttps ? "HTTPS" : "HTTP";

            logger.LogInformation("启动驱动: {Name} ({NameEN})", driverAttr.Name, driverAttr.NameEN);
            logger.LogInformation("型号: {Model}, 制造商: {Manufacturer}", driverAttr.Model, driverAttr.Manufacturer);
            logger.LogInformation("版本: {Version}", driverAttr.Version);

            // 创建驱动实例
            await using var driver = CreateDriverInstance<TDriver>(config);

            // 初始化驱动
            logger.LogInformation("初始化驱动...");
            var initialized = await driver.InitializeAsync();
            if (!initialized)
            {
                throw new InvalidOperationException($"驱动 {driverType.Name} 初始化失败");
            }

            // 创建请求处理器
            await using var handler = new RequestHandler(
                driver,
                options.CallbackUrl,
                options.CallbackTimeoutMs,
                options.EnableCallback,
                options.FunctionQueueCapacity,
                options.IdempotencyCapacity,
                options.ShutdownTimeoutMs);

            // 创建并启动服务器
            await using var server = new HttpsServer(options, handler);

            logger.LogInformation("启动{Scheme}服务器...", scheme);
            logger.LogInformation("监听端口: {Port}", options.Port);
            logger.LogInformation("协议: {Scheme}", scheme);
            if (options.UseHttps)
            {
                logger.LogInformation("TLS协议版本: {Protocol}", options.Protocol);
                logger.LogInformation("客户端证书验证: {ClientAuth}", options.ClientAuth);
            }
            logger.LogInformation("回调地址: {CallbackUrl}", options.CallbackUrl ?? "未配置");

            await server.StartAsync(cancellationToken);

            logger.LogInformation("服务器已启动，按Ctrl+C停止");

            // 等待取消信号
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => tcs.TrySetResult(true));

            // 处理控制台关闭事件
            ConsoleCancelEventHandler cancelHandler = (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("收到停止信号...");
                tcs.TrySetResult(true);
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                // 等待停止
                await tcs.Task;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }

            logger.LogInformation("正在停止服务器...");
            await server.StopAsync();

            logger.LogInformation("服务器已停止");
        }

        /// <summary>
        /// 创建驱动实例
        /// </summary>
        private static TDriver CreateDriverInstance<TDriver>(DeviceConfiguration config)
            where TDriver : DeviceDriverBase
        {
            var driverType = typeof(TDriver);

            // 尝试查找接受DeviceConfiguration的构造函数
            var constructor = driverType.GetConstructor(new[] { typeof(DeviceConfiguration) });
            if (constructor != null)
            {
                return (TDriver)constructor.Invoke(new object[] { config });
            }

            // 尝试无参构造函数
            constructor = driverType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                var driver = (TDriver)constructor.Invoke(null);
                driver.ApplyConfiguration(config);
                return driver;
            }

            throw new InvalidOperationException($"无法创建驱动实例 {driverType.Name}，请确保有公共构造函数");
        }

        /// <summary>
        /// 创建并返回驱动实例（不启动服务器）
        /// </summary>
        /// <typeparam name="TDriver">驱动类型</typeparam>
        /// <param name="config">设备配置</param>
        /// <returns>驱动实例</returns>
        public static TDriver CreateDriver<TDriver>(DeviceConfiguration config = null)
            where TDriver : DeviceDriverBase
        {
            return CreateDriverInstance<TDriver>(config ?? new DeviceConfiguration());
        }

        /// <summary>
        /// 创建HttpsOptions构建器
        /// </summary>
        public static HttpsOptionsBuilder CreateOptionsBuilder()
        {
            return new HttpsOptionsBuilder();
        }
    }

    /// <summary>
    /// HttpsOptions构建器
    /// 参照 IncubatorController 的 Spring Boot SSL 配置
    /// </summary>
    public class HttpsOptionsBuilder
    {
        private readonly HttpsOptions _options = new();

        /// <summary>
        /// 设置端口
        /// </summary>
        public HttpsOptionsBuilder WithPort(int port)
        {
            _options.Port = port;
            return this;
        }

        /// <summary>
        /// 设置主机名
        /// </summary>
        public HttpsOptionsBuilder WithHost(string host)
        {
            _options.Host = host;
            return this;
        }

        /// <summary>
        /// 显式启用HTTPS模式
        /// </summary>
        public HttpsOptionsBuilder UseHttps(bool useHttps = true)
        {
            _options.UseHttps = useHttps;
            return this;
        }

        /// <summary>
        /// 启用HTTP模式（无需证书）
        /// </summary>
        public HttpsOptionsBuilder UseHttp()
        {
            _options.UseHttps = false;
            _options.ClientAuth = ClientAuthMode.None;
            _options.RequireClientCertificate = false;
            return this;
        }

        /// <summary>
        /// 设置服务端证书（密钥库）
        /// 对应 Spring Boot: server.ssl.key-store-*
        /// </summary>
        public HttpsOptionsBuilder WithServerCertificate(string path, string password = null)
        {
            _options.ServerCertificatePath = path;
            _options.ServerCertificatePassword = password;
            return this;
        }

        /// <summary>
        /// 设置密钥别名
        /// 对应 Spring Boot: server.ssl.key-alias
        /// </summary>
        public HttpsOptionsBuilder WithKeyAlias(string alias)
        {
            _options.KeyAlias = alias;
            return this;
        }

        /// <summary>
        /// 设置密钥库类型
        /// 对应 Spring Boot: server.ssl.key-store-type
        /// </summary>
        public HttpsOptionsBuilder WithKeyStoreType(string type)
        {
            _options.KeyStoreType = type;
            return this;
        }

        /// <summary>
        /// 设置信任库
        /// 对应 Spring Boot: server.ssl.trust-store-*
        /// </summary>
        public HttpsOptionsBuilder WithTrustStore(string path, string password = null, string type = "PKCS12")
        {
            _options.TrustStorePath = path;
            _options.TrustStorePassword = password;
            _options.TrustStoreType = type;
            return this;
        }

        /// <summary>
        /// 设置TLS协议版本
        /// 对应 Spring Boot: server.ssl.protocol
        /// </summary>
        public HttpsOptionsBuilder WithProtocol(string protocol)
        {
            _options.Protocol = protocol;
            _options.EnabledProtocols = new[] { protocol };
            return this;
        }

        /// <summary>
        /// 设置启用的协议列表
        /// 对应 Spring Boot: server.ssl.enabled-protocols
        /// </summary>
        public HttpsOptionsBuilder WithEnabledProtocols(params string[] protocols)
        {
            _options.EnabledProtocols = protocols;
            return this;
        }

        /// <summary>
        /// 设置加密套件
        /// 对应 Spring Boot: server.ssl.ciphers
        /// </summary>
        public HttpsOptionsBuilder WithCiphers(params string[] ciphers)
        {
            _options.Ciphers = ciphers;
            return this;
        }

        /// <summary>
        /// 设置客户端认证模式
        /// 对应 Spring Boot: server.ssl.client-auth
        /// </summary>
        public HttpsOptionsBuilder WithClientAuth(ClientAuthMode mode)
        {
            _options.ClientAuth = mode;
            _options.RequireClientCertificate = mode != ClientAuthMode.None;
            return this;
        }

        /// <summary>
        /// 启用客户端证书验证
        /// </summary>
        public HttpsOptionsBuilder RequireClientCertificate(bool require = true)
        {
            _options.RequireClientCertificate = require;
            _options.ClientAuth = require ? ClientAuthMode.Need : ClientAuthMode.None;
            return this;
        }

        /// <summary>
        /// 添加受信任的客户端证书指纹
        /// </summary>
        public HttpsOptionsBuilder AddTrustedClientThumbprint(params string[] thumbprints)
        {
            _options.TrustedClientThumbprints = thumbprints;
            return this;
        }

        /// <summary>
        /// 添加受信任的CA指纹
        /// </summary>
        public HttpsOptionsBuilder AddTrustedIssuerThumbprint(params string[] thumbprints)
        {
            _options.TrustedIssuerThumbprints = thumbprints;
            return this;
        }

        /// <summary>
        /// 设置回调URL
        /// </summary>
        public HttpsOptionsBuilder WithCallbackUrl(string url)
        {
            _options.CallbackUrl = url;
            return this;
        }

        /// <summary>
        /// 设置回调超时时间
        /// </summary>
        public HttpsOptionsBuilder WithCallbackTimeout(int timeoutMs)
        {
            _options.CallbackTimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// 启用或禁用Function完成回调。
        /// </summary>
        public HttpsOptionsBuilder EnableCallback(bool enable = true)
        {
            _options.EnableCallback = enable;
            return this;
        }

        /// <summary>
        /// 构建HttpsOptions
        /// </summary>
        public HttpsOptions Build()
        {
            return _options;
        }
    }
}

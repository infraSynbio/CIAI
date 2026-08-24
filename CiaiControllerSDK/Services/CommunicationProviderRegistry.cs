using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using CiaiControllerSDK.Communication;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Interfaces;

namespace CiaiControllerSDK.Services
{
    /// <summary>内置连接和厂商连接的线程安全注册表。</summary>
    public static class CommunicationProviderRegistry
    {
        private static readonly ConcurrentDictionary<string, ICommunicationProvider> Providers =
            new(StringComparer.OrdinalIgnoreCase);

        static CommunicationProviderRegistry()
        {
            Register(new DelegateProvider(new[] { "tcp" }, ValidateTcp, c =>
                new TcpCommunication(c.Host, c.Port, c.ConnectTimeoutMs, c.ReadTimeoutMs, c.WriteTimeoutMs)));
            Register(new DelegateProvider(new[] { "http", "https" }, ValidateHttp, c =>
                new HttpCommunication(c.BaseUrl, c.ConnectTimeoutMs, c.Headers)));
            Register(new DelegateProvider(new[] { "serial" }, ValidateSerial, CreateSerial));
            Register(new DelegateProvider(new[] { "process", "legacy-process", "dll-process" },
                ValidateProcess, c => new ProcessCommunication(c)));
        }

        public static void Register(ICommunicationProvider provider, bool replace = false)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            foreach (var type in provider.Types ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(type)) continue;
                if (replace) Providers[type.Trim()] = provider;
                else if (!Providers.TryAdd(type.Trim(), provider))
                    throw new InvalidOperationException($"通信类型已注册: {type}");
            }
        }

        public static bool IsRegistered(string type) =>
            !string.IsNullOrWhiteSpace(type) && Providers.ContainsKey(type.Trim());

        public static ICommunication Create(ConnectionConfiguration configuration)
        {
            var provider = Validate(configuration);
            return provider.Create(configuration);
        }

        public static ICommunicationProvider Validate(ConnectionConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (!Providers.TryGetValue(configuration.Type?.Trim() ?? string.Empty, out var provider))
                throw new NotSupportedException(
                    $"未注册通信类型 '{configuration.Type}'。请实现ICommunicationProvider并在启动前注册。");
            ValidateCommon(configuration);
            provider.Validate(configuration);
            return provider;
        }

        private static void ValidateCommon(ConnectionConfiguration c)
        {
            if (string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("连接名称不能为空");
            if (c.ConnectTimeoutMs <= 0 || c.ReadTimeoutMs <= 0 || c.WriteTimeoutMs <= 0)
                throw new ArgumentException($"连接 {c.Name} 的超时必须大于0");
            if (c.ResourceWaitTimeoutMs <= 0 || c.EffectiveMaxConcurrency <= 0)
                throw new ArgumentException($"连接 {c.Name} 的资源参数必须大于0");
            if (c.RetryCount < 0 || c.RetryDelayMs < 0 || c.RetryBackoff < 1)
                throw new ArgumentException($"连接 {c.Name} 的重试参数无效");
        }

        private static void ValidateTcp(ConnectionConfiguration c)
        {
            if (string.IsNullOrWhiteSpace(c.Host) || c.Port is <= 0 or > 65535)
                throw new ArgumentException($"TCP连接 {c.Name} 必须配置host和1..65535端口");
        }

        private static void ValidateHttp(ConnectionConfiguration c)
        {
            if (!Uri.TryCreate(c.BaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"HTTP连接 {c.Name} 必须配置有效baseUrl");
        }

        private static void ValidateSerial(ConnectionConfiguration c)
        {
            if (string.IsNullOrWhiteSpace(c.SerialPort))
                throw new ArgumentException($"串口连接 {c.Name} 必须配置port");
        }

        private static void ValidateProcess(ConnectionConfiguration c)
        {
            if (string.IsNullOrWhiteSpace(c.Executable))
                throw new ArgumentException($"进程连接 {c.Name} 必须配置executable");
            if (!string.IsNullOrWhiteSpace(c.WorkingDirectory) && !Directory.Exists(c.WorkingDirectory))
                throw new ArgumentException($"进程连接 {c.Name} 的workingDirectory不存在: {Path.GetFullPath(c.WorkingDirectory)}");
            var hasPath = Path.IsPathRooted(c.Executable) ||
                          c.Executable.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                          c.Executable.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
            if (hasPath && !File.Exists(c.Executable))
                throw new ArgumentException($"进程连接 {c.Name} 的executable不存在: {Path.GetFullPath(c.Executable)}");
            var architecture = (c.Architecture ?? "auto").Trim().ToLowerInvariant();
            if (architecture is not ("auto" or "x86" or "x64" or "arm64"))
                throw new ArgumentException($"进程连接 {c.Name} 的architecture无效: {c.Architecture}");
            var apartment = (c.ApartmentState ?? "MTA").Trim().ToUpperInvariant();
            if (apartment is not ("STA" or "MTA"))
                throw new ArgumentException($"进程连接 {c.Name} 的apartmentState无效: {c.ApartmentState}");
            if (c.ShutdownTimeoutMs <= 0)
                throw new ArgumentException($"进程连接 {c.Name} 的shutdownTimeoutMs必须大于0");
        }

        private static ICommunication CreateSerial(ConnectionConfiguration c) =>
            new SerialCommunication(c.SerialPort, c.BaudRate, ParseParity(c.Parity), c.DataBits,
                ParseStopBits(c.StopBits), c.ReadTimeoutMs, c.WriteTimeoutMs, ParseEncoding(c.Encoding),
                ParseHandshake(c.FlowControl), c.DtrEnable, c.RtsEnable, c.DiscardInputBeforeWrite);

        private static Parity ParseParity(string value) => (value ?? "none").Trim().ToLowerInvariant() switch
        {
            "none" => Parity.None, "odd" => Parity.Odd, "even" => Parity.Even,
            "mark" => Parity.Mark, "space" => Parity.Space,
            _ => throw new ArgumentException($"不支持的串口校验方式: {value}")
        };

        private static StopBits ParseStopBits(double value)
        {
            if (Math.Abs(value - 1) < .001) return StopBits.One;
            if (Math.Abs(value - 1.5) < .001) return StopBits.OnePointFive;
            if (Math.Abs(value - 2) < .001) return StopBits.Two;
            throw new ArgumentException($"不支持的停止位: {value}");
        }

        private static Handshake ParseHandshake(string value) =>
            (value ?? "none").Trim().ToLowerInvariant() switch
            {
                "none" => Handshake.None,
                "xonxoff" or "xon/xoff" or "software" => Handshake.XOnXOff,
                "rtscts" or "rts/cts" or "hardware" => Handshake.RequestToSend,
                "rtscts+xonxoff" or "both" => Handshake.RequestToSendXOnXOff,
                _ => throw new ArgumentException($"不支持的流控方式: {value}")
            };

        private static Encoding ParseEncoding(string value) => (value ?? "utf-8").Trim().ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Encoding.UTF8, "ascii" => Encoding.ASCII,
            "unicode" or "utf-16" or "utf16" => Encoding.Unicode,
            "latin1" or "iso-8859-1" => Encoding.Latin1,
            _ => throw new ArgumentException($"不支持的串口编码: {value}")
        };

        private sealed class DelegateProvider : ICommunicationProvider
        {
            private readonly Action<ConnectionConfiguration> _validate;
            private readonly Func<ConnectionConfiguration, ICommunication> _create;
            public IEnumerable<string> Types { get; }
            public DelegateProvider(IEnumerable<string> types, Action<ConnectionConfiguration> validate,
                Func<ConnectionConfiguration, ICommunication> create)
            { Types = types; _validate = validate; _create = create; }
            public void Validate(ConnectionConfiguration configuration) => _validate(configuration);
            public ICommunication Create(ConnectionConfiguration configuration) => _create(configuration);
        }
    }
}

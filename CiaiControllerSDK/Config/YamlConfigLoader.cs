using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CiaiControllerSDK.Core;

namespace CiaiControllerSDK.Config
{
    /// <summary>
    /// YAML配置文件加载器
    /// </summary>
    public static class YamlConfigLoader
    {
        private static readonly Regex EnvironmentVariablePattern = new(
            @"\$\{([A-Za-z_][A-Za-z0-9_]*)(?::-([^}]*))?\}", RegexOptions.Compiled);
        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            // Preserve YAML scalar types in device.settings/additional settings so a
            // declarative bool/int can be converted into a strongly typed options model.
            .WithAttemptingUnquotedStringTypeDeserialization()
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <param name="configPath">配置文件路径，默认application.yml</param>
        /// <returns>驱动配置</returns>
        public static DriverConfig Load(string configPath = "application.yml")
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"配置文件不存在: {configPath}");
            }

            var fullPath = Path.GetFullPath(configPath);
            var yaml = File.ReadAllText(fullPath);
            var config = Parse(yaml);
            config.SourceDirectory = Path.GetDirectoryName(fullPath);
            ResolveKnownRelativePaths(config, Path.GetDirectoryName(fullPath));
            return config;
        }

        private static void ResolveKnownRelativePaths(DriverConfig config, string configDirectory)
        {
            if (config == null || string.IsNullOrWhiteSpace(configDirectory)) return;
            if (!string.IsNullOrWhiteSpace(config.Server?.Certificate?.Path))
                config.Server.Certificate.Path = ResolvePath(configDirectory, config.Server.Certificate.Path);
            if (!string.IsNullOrWhiteSpace(config.Server?.TrustStore?.Path))
                config.Server.TrustStore.Path = ResolvePath(configDirectory, config.Server.TrustStore.Path);

            if (config.Device?.Connections == null) return;
            foreach (var source in config.Device.Connections.Values.Where(value => value != null))
            {
                if (!string.IsNullOrWhiteSpace(source.WorkingDirectory))
                    source.WorkingDirectory = ResolvePath(configDirectory, source.WorkingDirectory);
                if (string.IsNullOrWhiteSpace(source.Executable) || Path.IsPathRooted(source.Executable))
                    continue;

                var hasSeparator = source.Executable.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                                   source.Executable.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
                var candidate = hasSeparator
                    ? Path.Combine(configDirectory, source.Executable)
                    : !string.IsNullOrWhiteSpace(source.WorkingDirectory)
                        ? Path.Combine(source.WorkingDirectory, source.Executable)
                        : null;
                if (candidate != null && (hasSeparator || File.Exists(candidate)))
                    source.Executable = Path.GetFullPath(candidate);
            }
        }

        private static string ResolvePath(string baseDirectory, string value) =>
            Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(baseDirectory, value));

        /// <summary>
        /// 从字符串解析配置
        /// </summary>
        /// <param name="yamlContent">YAML内容</param>
        /// <returns>驱动配置</returns>
        public static DriverConfig Parse(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                throw new ArgumentException("YAML内容不能为空", nameof(yamlContent));
            }

            var expandedYaml = ExpandEnvironmentVariables(yamlContent);
            var config = Deserializer.Deserialize<DriverConfig>(expandedYaml) ?? new DriverConfig();
            CaptureAdditionalDeviceSettings(config, expandedYaml);
            return config;
        }

        private static string ExpandEnvironmentVariables(string content)
        {
            var lines = content.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var commentIndex = FindYamlCommentIndex(line);
                var yaml = commentIndex < 0 ? line : line.Substring(0, commentIndex);
                var comment = commentIndex < 0 ? string.Empty : line.Substring(commentIndex);
                lines[index] = EnvironmentVariablePattern.Replace(yaml, ExpandEnvironmentMatch) + comment;
            }
            return string.Join("\n", lines);
        }

        private static string ExpandEnvironmentMatch(Match match)
        {
            var value = Environment.GetEnvironmentVariable(match.Groups[1].Value);
            if (value != null) return value;
            if (match.Groups[2].Success) return match.Groups[2].Value;
            throw new InvalidOperationException($"必需的环境变量未设置: {match.Groups[1].Value}");
        }

        private static int FindYamlCommentIndex(string line)
        {
            var inSingleQuote = false;
            var inDoubleQuote = false;
            var escaped = false;
            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (inDoubleQuote)
                {
                    if (escaped) { escaped = false; continue; }
                    if (character == '\\') { escaped = true; continue; }
                    if (character == '"') inDoubleQuote = false;
                    continue;
                }
                if (inSingleQuote)
                {
                    if (character != '\'') continue;
                    if (index + 1 < line.Length && line[index + 1] == '\'') { index++; continue; }
                    inSingleQuote = false;
                    continue;
                }
                if (character == '"') { inDoubleQuote = true; continue; }
                if (character == '\'') { inSingleQuote = true; continue; }
                if (character == '#' && (index == 0 || char.IsWhiteSpace(line[index - 1])))
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// 尝试加载配置，失败返回null
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <param name="config">加载的配置</param>
        /// <returns>是否成功</returns>
        public static bool TryLoad(string configPath, out DriverConfig config)
        {
            config = null;
            try
            {
                config = Load(configPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将DriverConfig转换为HttpsOptions
        /// 参照 IncubatorController 的 Spring Boot SSL 配置
        /// </summary>
        /// <param name="config">驱动配置</param>
        /// <returns>服务器选项</returns>
        public static WebServer.HttpsOptions ToHttpsOptions(this DriverConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // server段可省略：使用可直接启动的HTTP默认值。
            config.Server ??= new ServerConfig();

            var options = new WebServer.HttpsOptions
            {
                Port = config.Server.Port,
                Host = config.Server.Host ?? "localhost",
                UseHttps = config.Server.UseHttps,
                MaxConcurrentRequests = config.Server.MaxConcurrentRequests,
                MaxRequestBodyBytes = config.Server.MaxRequestBodyBytes,
                FunctionQueueCapacity = config.Server.FunctionQueueCapacity,
                IdempotencyCapacity = config.Server.IdempotencyCapacity,
                ShutdownTimeoutMs = config.Server.ShutdownTimeoutMs,
                AllowReplaceCertificateBinding = config.Server.AllowReplaceCertificateBinding
            };

            // 密钥库配置（对应 Spring Boot: server.ssl.key-store-*）
            if (config.Server.Certificate != null)
            {
                options.ServerCertificatePath = config.Server.Certificate.Path;
                options.ServerCertificatePassword = config.Server.Certificate.Password;
                options.KeyStoreType = config.Server.Certificate.Type ?? "PKCS12";
                options.KeyAlias = config.Server.Certificate.Alias;
            }

            // 信任库配置（对应 Spring Boot: server.ssl.trust-store-*）
            if (config.Server.TrustStore != null)
            {
                options.TrustStorePath = config.Server.TrustStore.Path;
                options.TrustStorePassword = config.Server.TrustStore.Password;
                options.TrustStoreType = config.Server.TrustStore.Type ?? "PKCS12";
            }

            // SSL/TLS 配置（对应 Spring Boot: server.ssl.protocol, ciphers, enabled-protocols）
            if (config.Server.Ssl != null)
            {
                options.Protocol = config.Server.Ssl.Protocol ?? "TLSv1.2";
                options.EnabledProtocols = config.Server.Ssl.EnabledProtocols ?? new[] { "TLSv1.2" };
                options.Ciphers = config.Server.Ssl.Ciphers ?? Array.Empty<string>();
            }

            // 客户端认证配置（对应 Spring Boot: server.ssl.client-auth）
            if (config.Server.ClientAuth != null)
            {
                var mode = config.Server.ClientAuth.Enabled
                    ? config.Server.ClientAuth.Mode?.ToLower() ?? "need"
                    : "none";
                options.ClientAuth = mode switch
                {
                    "need" => WebServer.ClientAuthMode.Need,
                    "want" => WebServer.ClientAuthMode.Want,
                    "none" => WebServer.ClientAuthMode.None,
                    _ => throw new ArgumentException(
                        $"不支持的客户端认证模式: {config.Server.ClientAuth.Mode}")
                };
                options.RequireClientCertificate = options.ClientAuth != WebServer.ClientAuthMode.None;
                options.TrustedClientThumbprints = config.Server.ClientAuth.TrustedThumbprints;
                options.TrustedIssuerThumbprints = config.Server.ClientAuth.TrustedIssuers;
            }

            // 回调配置
            options.CallbackUrl = config.Callback?.Url;
            options.CallbackTimeoutMs = config.Callback?.TimeoutMs ?? 30000;
            options.EnableCallback = config.Callback?.Enabled ?? false;

            return options;
        }

        /// <summary>
        /// 将DriverConfig转换为DeviceConfiguration
        /// </summary>
        /// <param name="config">驱动配置</param>
        /// <returns>设备配置</returns>
        public static Core.DeviceConfiguration ToDeviceConfiguration(this DriverConfig config)
        {
            var deviceConfig = new Core.DeviceConfiguration
            {
                // Do not invent a different identity on every restart. Missing IDs stay visible
                // to configuration diagnostics and can still be tolerated by development drivers.
                DeviceId = config.Device?.DeviceId,
                DeviceCallResources = config.Device?.DeviceCallResources ?? 1,
                DeviceCallTimeout = config.Device?.DeviceCallTimeoutMs ?? 30000
            };

            if (config.Device?.CommunicationType != null)
            {
                deviceConfig.CommunicationType = config.Device.CommunicationType.ToUpper() switch
                {
                    "TCP" => Core.CommunicationType.TCP,
                    "HTTP" => Core.CommunicationType.HTTP,
                    "SERIAL" => Core.CommunicationType.Serial,
                    "DLL" => Core.CommunicationType.DLL,
                    _ => throw new ArgumentException(
                        $"不支持的设备通信类型: {config.Device.CommunicationType}")
                };
            }

            if (config.Device?.Tcp != null)
            {
                deviceConfig.Host = config.Device.Tcp.Host;
                deviceConfig.Port = config.Device.Tcp.Port;
                deviceConfig.ConnectionTimeout = config.Device.Tcp.ConnectTimeoutMs ?? config.Device.Tcp.TimeoutMs;
                deviceConfig.ReadTimeout = config.Device.Tcp.ReadTimeoutMs ?? config.Device.Tcp.TimeoutMs;
                deviceConfig.WriteTimeout = config.Device.Tcp.WriteTimeoutMs ?? config.Device.Tcp.TimeoutMs;
            }

            if (config.Device?.Http != null)
            {
                deviceConfig.BaseUrl = config.Device.Http.BaseUrl;
                deviceConfig.ConnectionTimeout = config.Device.Http.TimeoutMs;
            }

            if (config.Device?.Serial != null)
            {
                deviceConfig.SerialPort = config.Device.Serial.Port;
                deviceConfig.BaudRate = config.Device.Serial.BaudRate;
                deviceConfig.DataBits = config.Device.Serial.DataBits;
                deviceConfig.StopBits = config.Device.Serial.StopBits;
                deviceConfig.Parity = config.Device.Serial.Parity ?? "none";
                deviceConfig.Encoding = config.Device.Serial.Encoding ?? "utf-8";
                deviceConfig.ConnectionTimeout = config.Device.Serial.TimeoutMs;
                deviceConfig.ReadTimeout = config.Device.Serial.ReadTimeoutMs ?? config.Device.Serial.TimeoutMs;
                deviceConfig.WriteTimeout = config.Device.Serial.WriteTimeoutMs ?? config.Device.Serial.TimeoutMs;
                deviceConfig.FlowControl = config.Device.Serial.FlowControl ?? "none";
                deviceConfig.DtrEnable = config.Device.Serial.DtrEnable;
                deviceConfig.RtsEnable = config.Device.Serial.RtsEnable;
                deviceConfig.DiscardInputBeforeWrite = config.Device.Serial.DiscardInputBeforeWrite;
            }

            if (config.Device?.Connections != null)
            {
                foreach (var pair in config.Device.Connections)
                {
                    var source = pair.Value ?? throw new ArgumentException(
                        $"device.connections.{pair.Key}不能为空");
                    deviceConfig.Connections[pair.Key] = new ConnectionConfiguration
                    {
                        Name = pair.Key,
                        Type = source.Type,
                        IsDefault = source.Default,
                        Required = source.Required,
                        ConnectOnStart = source.ConnectOnStart,
                        Host = source.Host,
                        Port = source.Port,
                        BaseUrl = source.BaseUrl,
                        SerialPort = source.SerialPort,
                        BaudRate = source.BaudRate,
                        DataBits = source.DataBits,
                        StopBits = source.StopBits,
                        Parity = source.Parity,
                        Encoding = source.Encoding,
                        FlowControl = source.FlowControl,
                        DtrEnable = source.DtrEnable,
                        RtsEnable = source.RtsEnable,
                        DiscardInputBeforeWrite = source.DiscardInputBeforeWrite,
                        ConnectTimeoutMs = source.ConnectTimeoutMs,
                        ReadTimeoutMs = source.ReadTimeoutMs,
                        WriteTimeoutMs = source.WriteTimeoutMs,
                        ResourceWaitTimeoutMs = source.ResourceWaitTimeoutMs,
                        MaxConcurrency = source.MaxConcurrency,
                        ResourceGroup = source.ResourceGroup,
                        RetryCount = source.RetryCount,
                        RetryDelayMs = source.RetryDelayMs,
                        RetryBackoff = source.RetryBackoff,
                        Executable = source.Executable,
                        Arguments = source.Arguments,
                        WorkingDirectory = source.WorkingDirectory,
                        Architecture = source.Architecture,
                        Framework = source.Framework,
                        ApartmentState = source.ApartmentState,
                        ShutdownTimeoutMs = source.ShutdownTimeoutMs,
                        Environment = source.Environment,
                        Headers = source.Headers,
                        Settings = source.Settings
                    };
                }
            }

            if (config.Device?.Settings != null)
            {
                foreach (var setting in config.Device.Settings)
                {
                    deviceConfig.ExtraSettings[setting.Key] = setting.Value;
                }
            }

            if (config.Device?.AdditionalSettings != null)
            {
                foreach (var setting in config.Device.AdditionalSettings)
                {
                    deviceConfig.ExtraSettings[setting.Key] = setting.Value;
                }
            }

            deviceConfig.ConfigurationDirectory = config.SourceDirectory;

            return deviceConfig;
        }

        private static void CaptureAdditionalDeviceSettings(DriverConfig config, string yamlContent)
        {
            if (config?.Device == null)
                return;

            var root = Deserializer.Deserialize<Dictionary<object, object>>(yamlContent);
            if (root == null)
                return;

            var deviceEntry = root.FirstOrDefault(entry =>
                string.Equals(entry.Key?.ToString(), "device", StringComparison.OrdinalIgnoreCase));

            if (deviceEntry.Value is not IDictionary<object, object> deviceMap)
                return;

            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "deviceId", "communicationType", "tcp", "http", "serial", "connections", "settings",
                "deviceCallResources", "deviceCallTimeoutMs"
            };

            foreach (var entry in deviceMap)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key) || knownKeys.Contains(key))
                    continue;

                config.Device.AdditionalSettings[key] = NormalizeYamlValue(entry.Value);
            }
        }

        private static object NormalizeYamlValue(object value)
        {
            if (value is IDictionary<object, object> dictionary)
            {
                return dictionary.ToDictionary(
                    entry => entry.Key?.ToString() ?? string.Empty,
                    entry => NormalizeYamlValue(entry.Value),
                    StringComparer.OrdinalIgnoreCase);
            }

            if (value is IList list)
            {
                return list.Cast<object>().Select(NormalizeYamlValue).ToList();
            }

            return value;
        }
    }
}

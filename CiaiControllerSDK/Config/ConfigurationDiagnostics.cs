using System;
using System.Collections.Generic;
using System.Linq;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Services;
using CiaiControllerSDK.WebServer;

namespace CiaiControllerSDK.Config
{
    public enum DiagnosticSeverity { Info, Warning, Error }
    public sealed class ConfigurationDiagnostic
    {
        public DiagnosticSeverity Severity { get; init; }
        public string Path { get; init; }
        public string Message { get; init; }
        public override string ToString() => $"{Severity}: {Path}: {Message}";
    }

    public sealed class ConfigurationValidationException : Exception
    {
        public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }
        public ConfigurationValidationException(IEnumerable<ConfigurationDiagnostic> diagnostics)
            : base(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()))) =>
            Diagnostics = diagnostics.ToArray();
    }

    /// <summary>不连接设备的配置预检结果，可用于安装程序、CI和“检查配置”命令。</summary>
    public sealed class ConfigurationValidationReport
    {
        public string ConfigPath { get; }
        public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);
        public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

        public ConfigurationValidationReport(string configPath,
            IEnumerable<ConfigurationDiagnostic> diagnostics)
        {
            ConfigPath = configPath;
            Diagnostics = (diagnostics ?? Array.Empty<ConfigurationDiagnostic>()).ToArray();
        }

        public void ThrowIfInvalid()
        {
            var errors = Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new ConfigurationValidationException(errors);
        }
    }

    /// <summary>启动前配置体检，错误精确到YAML路径。</summary>
    public static class ConfigurationValidator
    {
        public static IReadOnlyList<ConfigurationDiagnostic> Validate(HttpsOptions server,
            DeviceConfiguration configuration)
        {
            var result = new List<ConfigurationDiagnostic>();
            if (server == null)
            {
                result.Add(Error("server", "服务器配置不能为空"));
            }
            else
            {
                try { server.Validate(); }
                catch (Exception ex) { result.Add(Error("server", ex.Message)); }
            }
            result.AddRange(Validate(configuration));
            return result;
        }

        public static IReadOnlyList<ConfigurationDiagnostic> Validate(DeviceConfiguration configuration)
        {
            var result = new List<ConfigurationDiagnostic>();
            if (configuration == null)
            {
                result.Add(Error("device", "设备配置不能为空"));
                return result;
            }
            if (configuration.DeviceCallResources <= 0)
                result.Add(Error("device.deviceCallResources", "必须大于0"));
            if (configuration.DeviceCallTimeout <= 0)
                result.Add(Error("device.deviceCallTimeoutMs", "必须大于0"));

            if (string.IsNullOrWhiteSpace(configuration.DeviceId))
                result.Add(Warning("device.deviceId", "未配置设备ID；建议为生产设备设置稳定且唯一的ID"));

            var connections = configuration.Connections?.Values.ToList() ?? new List<ConnectionConfiguration>();
            if (connections.Count == 0)
            {
                ValidateLegacyConnection(configuration, result);
                return result;
            }
            if (connections.Count(c => c.IsDefault) > 1)
                result.Add(Error("device.connections", "只能有一个default连接"));
            foreach (var c in connections)
            {
                var path = $"device.connections.{c.Name}";
                try { CommunicationProviderRegistry.Validate(c); }
                catch (Exception ex) { result.Add(Error(path, ex.Message)); }
                if (c.EffectiveMaxConcurrency == 1 && c.MaxConcurrency > 1)
                    result.Add(Warning(path + ".maxConcurrency", "该单通道协议固定按1串行执行"));
            }
            foreach (var group in connections.Where(c => !string.IsNullOrWhiteSpace(c.ResourceGroup))
                         .GroupBy(c => c.ResourceGroup, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Select(c => c.EffectiveMaxConcurrency).Distinct().Count() > 1)
                    result.Add(Error("device.connections", $"资源组 {group.Key} 的maxConcurrency必须一致"));
            }
            return result;
        }

        private static void ValidateLegacyConnection(DeviceConfiguration configuration,
            ICollection<ConfigurationDiagnostic> result)
        {
            if (configuration.CommunicationType == CommunicationType.DLL)
                return;

            var connection = new ConnectionConfiguration
            {
                Name = "default",
                Type = configuration.CommunicationType switch
                {
                    CommunicationType.TCP => "tcp",
                    CommunicationType.HTTP => "http",
                    CommunicationType.Serial => "serial",
                    _ => string.Empty
                },
                Host = configuration.Host,
                Port = configuration.Port,
                BaseUrl = configuration.BaseUrl,
                SerialPort = configuration.SerialPort,
                BaudRate = configuration.BaudRate,
                DataBits = configuration.DataBits,
                StopBits = configuration.StopBits,
                Parity = configuration.Parity,
                Encoding = configuration.Encoding,
                FlowControl = configuration.FlowControl,
                DtrEnable = configuration.DtrEnable,
                RtsEnable = configuration.RtsEnable,
                DiscardInputBeforeWrite = configuration.DiscardInputBeforeWrite,
                ConnectTimeoutMs = configuration.ConnectionTimeout,
                ReadTimeoutMs = configuration.ReadTimeout,
                WriteTimeoutMs = configuration.WriteTimeout,
                ResourceWaitTimeoutMs = configuration.DeviceCallTimeout,
                MaxConcurrency = 1
            };

            try
            {
                CommunicationProviderRegistry.Validate(connection);
            }
            catch (Exception ex)
            {
                var section = configuration.CommunicationType.ToString().ToLowerInvariant();
                result.Add(Error($"device.{section}", ex.Message));
            }
        }

        public static void ValidateAndThrow(DeviceConfiguration configuration)
        {
            var errors = Validate(configuration).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new ConfigurationValidationException(errors);
        }
        public static void ValidateAndThrow(HttpsOptions server, DeviceConfiguration configuration)
        {
            var errors = Validate(server, configuration)
                .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new ConfigurationValidationException(errors);
        }
        private static ConfigurationDiagnostic Error(string path, string message) =>
            new() { Severity = DiagnosticSeverity.Error, Path = path, Message = message };
        private static ConfigurationDiagnostic Warning(string path, string message) =>
            new() { Severity = DiagnosticSeverity.Warning, Path = path, Message = message };
    }
}

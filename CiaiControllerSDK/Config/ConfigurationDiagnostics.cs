using System;
using System.Collections.Generic;
using System.Linq;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Services;

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

    /// <summary>启动前配置体检，错误精确到YAML路径。</summary>
    public static class ConfigurationValidator
    {
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

            var connections = configuration.Connections?.Values.ToList() ?? new List<ConnectionConfiguration>();
            if (connections.Count == 0) return result;
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

        public static void ValidateAndThrow(DeviceConfiguration configuration)
        {
            var errors = Validate(configuration).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new ConfigurationValidationException(errors);
        }
        private static ConfigurationDiagnostic Error(string path, string message) =>
            new() { Severity = DiagnosticSeverity.Error, Path = path, Message = message };
        private static ConfigurationDiagnostic Warning(string path, string message) =>
            new() { Severity = DiagnosticSeverity.Warning, Path = path, Message = message };
    }
}

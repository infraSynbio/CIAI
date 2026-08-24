using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CiaiControllerSDK.Core
{
    /// <summary>
    /// 设备配置
    /// </summary>
    public class DeviceConfiguration
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 通信类型: TCP, HTTP, Serial, DLL
        /// </summary>
        public CommunicationType CommunicationType { get; set; }

        /// <summary>
        /// TCP主机地址
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// TCP端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// HTTP基础URL
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// 串口名称
        /// </summary>
        public string SerialPort { get; set; }

        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 串口数据位
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 串口停止位（1、1.5或2）
        /// </summary>
        public double StopBits { get; set; } = 1;

        /// <summary>
        /// 串口校验方式（none、odd、even、mark、space）
        /// </summary>
        public string Parity { get; set; } = "none";

        /// <summary>
        /// 串口文本编码（utf-8、ascii、unicode、latin1）。二进制协议应直接使用byte[]。
        /// </summary>
        public string Encoding { get; set; } = "utf-8";
        public string FlowControl { get; set; } = "none";
        public bool DtrEnable { get; set; }
        public bool RtsEnable { get; set; }
        public bool DiscardInputBeforeWrite { get; set; }

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// 读取超时（毫秒）
        /// </summary>
        public int ReadTimeout { get; set; } = 10000;

        /// <summary>
        /// 写入超时（毫秒）
        /// </summary>
        public int WriteTimeout { get; set; } = 10000;

        /// <summary>
        /// 兼容旧驱动的统一读写超时。设置时会同时更新读取和写入超时。
        /// </summary>
        public int ReadWriteTimeout
        {
            get => Math.Max(ReadTimeout, WriteTimeout);
            set
            {
                ReadTimeout = value;
                WriteTimeout = value;
            }
        }

        /// <summary>
        /// 底层设备/DLL调用允许的并发数。与FunctionalResources和Parallelizability相互独立。
        /// </summary>
        public int DeviceCallResources { get; set; } = 1;

        /// <summary>
        /// 等待底层设备调用资源的超时时间（毫秒）。
        /// </summary>
        public int DeviceCallTimeout { get; set; } = 30000;

        /// <summary>
        /// 命名连接。非空时优先使用；旧版communicationType/tcp/http/serial配置继续兼容。
        /// </summary>
        public Dictionary<string, ConnectionConfiguration> Connections { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 额外配置
        /// </summary>
        public Dictionary<string, object> ExtraSettings { get; set; } = new();

        /// <summary>
        /// 获取自定义配置；类型不匹配或不存在时返回默认值。
        /// </summary>
        public T GetExtraSetting<T>(string key, T defaultValue = default)
        {
            if (!TryGetSettingValue(key, out var value) || value == null)
                return defaultValue;

            if (value is T typedValue)
            {
                return typedValue;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                if (targetType.IsEnum)
                    return (T)Enum.Parse(targetType, value.ToString(), true);
                if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                    return (T)Convert.ChangeType(value, targetType);
                var json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize<T>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>获取必需配置；缺失或类型错误时返回明确的配置路径。</summary>
        public T GetRequiredExtraSetting<T>(string key)
        {
            if (!TryGetSettingValue(key, out var value) || value == null)
                throw new InvalidOperationException($"缺少必需配置: device.settings.{key}");
            try
            {
                if (value is T typed) return typed;
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                if (targetType.IsEnum)
                    return (T)Enum.Parse(targetType, value.ToString(), true);
                if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                    return (T)Convert.ChangeType(value, targetType);
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"配置 device.settings.{key} 不能转换为 {typeof(T).Name}", ex);
            }
        }

        private bool TryGetSettingValue(string key, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key) || ExtraSettings == null) return false;
            object current = ExtraSettings;
            foreach (var segment in key.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current is IDictionary<string, object> dictionary)
                {
                    var pair = dictionary.FirstOrDefault(p =>
                        string.Equals(p.Key, segment, StringComparison.OrdinalIgnoreCase));
                    if (pair.Key == null) return false;
                    current = pair.Value;
                }
                else return false;
            }
            value = current;
            return true;
        }

        public void SetExtraSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("配置键不能为空", nameof(key));

            ExtraSettings ??= new Dictionary<string, object>();
            ExtraSettings[key] = value;
        }

        /// <summary>
        /// 创建TCP配置
        /// </summary>
        public static DeviceConfiguration CreateTcp(string deviceId, string host, int port, int timeout = 5000)
        {
            return new DeviceConfiguration
            {
                DeviceId = deviceId,
                CommunicationType = CommunicationType.TCP,
                Host = host,
                Port = port,
                ConnectionTimeout = timeout,
                ReadTimeout = timeout,
                WriteTimeout = timeout
            };
        }

        /// <summary>
        /// 创建HTTP配置
        /// </summary>
        public static DeviceConfiguration CreateHttp(string deviceId, string baseUrl, int timeout = 30000)
        {
            return new DeviceConfiguration
            {
                DeviceId = deviceId,
                CommunicationType = CommunicationType.HTTP,
                BaseUrl = baseUrl,
                ConnectionTimeout = timeout
            };
        }

        /// <summary>
        /// 创建串口配置
        /// </summary>
        public static DeviceConfiguration CreateSerial(string deviceId, string port, int baudRate = 9600,
            int dataBits = 8, double stopBits = 1, string parity = "none", int timeout = 5000,
            string encoding = "utf-8")
        {
            return new DeviceConfiguration
            {
                DeviceId = deviceId,
                CommunicationType = CommunicationType.Serial,
                SerialPort = port,
                BaudRate = baudRate,
                DataBits = dataBits,
                StopBits = stopBits,
                Parity = parity,
                Encoding = encoding,
                ConnectionTimeout = timeout,
                ReadTimeout = timeout,
                WriteTimeout = timeout
            };
        }

        /// <summary>
        /// 创建厂商DLL/COM配置。
        /// </summary>
        public static DeviceConfiguration CreateDll(string deviceId)
        {
            return new DeviceConfiguration
            {
                DeviceId = deviceId,
                CommunicationType = CommunicationType.DLL
            };
        }
    }

    /// <summary>
    /// 通信类型
    /// </summary>
    public enum CommunicationType
    {
        TCP,
        HTTP,
        Serial,
        DLL
    }
}

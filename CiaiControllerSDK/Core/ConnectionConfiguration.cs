using System;
using System.Collections.Generic;

namespace CiaiControllerSDK.Core
{
    /// <summary>
    /// 一个可命名的底层连接。驱动可以同时声明 TCP、串口、HTTP 或厂商进程桥接连接。
    /// </summary>
    public sealed class ConnectionConfiguration
    {
        public string Name { get; set; } = "default";
        public string Type { get; set; } = "tcp";
        public bool IsDefault { get; set; }
        public bool Required { get; set; } = true;
        public bool ConnectOnStart { get; set; } = true;

        public string Host { get; set; }
        public int Port { get; set; }
        public string BaseUrl { get; set; }

        public string SerialPort { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public double StopBits { get; set; } = 1;
        public string Parity { get; set; } = "none";
        public string Encoding { get; set; } = "utf-8";
        public string FlowControl { get; set; } = "none";
        public bool DtrEnable { get; set; }
        public bool RtsEnable { get; set; }
        public bool DiscardInputBeforeWrite { get; set; }

        public int ConnectTimeoutMs { get; set; } = 5000;
        public int ReadTimeoutMs { get; set; } = 10000;
        public int WriteTimeoutMs { get; set; } = 10000;
        public int ResourceWaitTimeoutMs { get; set; } = 30000;

        /// <summary>HTTP/DLL/API连接允许的并发数。TCP/串口始终按1执行。</summary>
        public int MaxConcurrency { get; set; } = 1;
        /// <summary>多个连接填写同一名称时，共享同一个底层资源信号量。</summary>
        public string ResourceGroup { get; set; }

        public int RetryCount { get; set; }
        public int RetryDelayMs { get; set; } = 200;
        public double RetryBackoff { get; set; } = 2;

        /// <summary>厂商DLL/COM桥接进程路径。适配器通过stdin/stdout长度前缀帧通信。</summary>
        public string Executable { get; set; }
        public IList<string> Arguments { get; set; } = new List<string>();
        public string WorkingDirectory { get; set; }
        public string Architecture { get; set; } = "auto";
        public string Framework { get; set; }
        public string ApartmentState { get; set; } = "MTA";
        public int ShutdownTimeoutMs { get; set; } = 5000;
        public IDictionary<string, string> Environment { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, string> Headers { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, object> Settings { get; set; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public int EffectiveMaxConcurrency
        {
            get
            {
                var type = (Type ?? string.Empty).Trim().ToLowerInvariant();
                return type is "tcp" or "serial" or "modbus" or "modbus-tcp" or "modbus-rtu"
                    ? 1
                    : Math.Max(1, MaxConcurrency);
            }
        }
    }
}

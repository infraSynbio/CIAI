using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace CiaiControllerSDK.Config
{
    /// <summary>
    /// 驱动配置模型，对应application.yml文件结构
    /// 参照 IncubatorController 的 Spring Boot SSL 配置
    /// </summary>
    public class DriverConfig
    {
        /// <summary>
        /// 服务器配置
        /// </summary>
        public ServerConfig Server { get; set; }

        /// <summary>
        /// 回调配置
        /// </summary>
        public CallbackConfig Callback { get; set; }

        /// <summary>
        /// 设备配置
        /// </summary>
        public DeviceConfigSection Device { get; set; }
    }

    /// <summary>
    /// 服务器配置
    /// </summary>
    public class ServerConfig
    {
        public int MaxConcurrentRequests { get; set; } = 100;
        public int MaxRequestBodyBytes { get; set; } = 1024 * 1024;
        public int FunctionQueueCapacity { get; set; } = 100;
        public int IdempotencyCapacity { get; set; } = 10000;
        public int ShutdownTimeoutMs { get; set; } = 30000;
        public bool AllowReplaceCertificateBinding { get; set; }
        /// <summary>
        /// 监听端口
        /// </summary>
        public int Port { get; set; } = 443;

        /// <summary>
        /// 主机名
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// 是否使用HTTPS，默认true
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// 证书配置（密钥库）
        /// 对应 Spring Boot: server.ssl.key-store-*
        /// </summary>
        public CertificateConfig Certificate { get; set; }

        /// <summary>
        /// 信任库配置
        /// 对应 Spring Boot: server.ssl.trust-store-*
        /// </summary>
        public TrustStoreConfig TrustStore { get; set; }

        /// <summary>
        /// SSL/TLS 配置
        /// 对应 Spring Boot: server.ssl.*
        /// </summary>
        public SslConfig Ssl { get; set; }

        /// <summary>
        /// 客户端认证配置
        /// 对应 Spring Boot: server.ssl.client-auth
        /// </summary>
        public ClientAuthConfig ClientAuth { get; set; }
    }

    /// <summary>
    /// 证书配置（密钥库）
    /// 对应 Spring Boot: server.ssl.key-store-*
    /// </summary>
    public class CertificateConfig
    {
        /// <summary>
        /// 证书文件路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 证书密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 密钥库类型，默认 PKCS12
        /// </summary>
        public string Type { get; set; } = "PKCS12";

        /// <summary>
        /// 密钥别名
        /// </summary>
        public string Alias { get; set; }
    }

    /// <summary>
    /// 信任库配置
    /// 对应 Spring Boot: server.ssl.trust-store-*
    /// </summary>
    public class TrustStoreConfig
    {
        /// <summary>
        /// 信任库文件路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 信任库密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 信任库类型，默认 PKCS12
        /// </summary>
        public string Type { get; set; } = "PKCS12";
    }

    /// <summary>
    /// SSL/TLS 配置
    /// 对应 Spring Boot: server.ssl.protocol, ciphers, enabled-protocols
    /// </summary>
    public class SslConfig
    {
        /// <summary>
        /// SSL协议版本，默认 TLSv1.3
        /// 对应 Spring Boot: server.ssl.protocol
        /// </summary>
        public string Protocol { get; set; } = "TLSv1.3";

        /// <summary>
        /// 启用的协议列表
        /// 对应 Spring Boot: server.ssl.enabled-protocols
        /// </summary>
        public string[] EnabledProtocols { get; set; }

        /// <summary>
        /// 加密套件列表
        /// 对应 Spring Boot: server.ssl.ciphers
        /// </summary>
        public string[] Ciphers { get; set; }
    }

    /// <summary>
    /// 客户端认证配置
    /// 对应 Spring Boot: server.ssl.client-auth
    /// </summary>
    public class ClientAuthConfig
    {
        /// <summary>
        /// 客户端认证模式: need, want, none
        /// 对应 Spring Boot: server.ssl.client-auth
        /// </summary>
        public string Mode { get; set; } = "need";

        /// <summary>
        /// 是否启用客户端证书验证
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 受信任的客户端证书指纹列表
        /// </summary>
        public string[] TrustedThumbprints { get; set; }

        /// <summary>
        /// 受信任的CA指纹列表
        /// </summary>
        public string[] TrustedIssuers { get; set; }
    }

    /// <summary>
    /// 回调配置
    /// </summary>
    public class CallbackConfig
    {
        /// <summary>
        /// 回调URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 回调超时时间（毫秒）
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 是否启用回调
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 设备配置部分
    /// </summary>
    public class DeviceConfigSection
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 通信类型: TCP, HTTP, Serial, DLL
        /// </summary>
        public string CommunicationType { get; set; }

        /// <summary>
        /// TCP配置
        /// </summary>
        public TcpConfig Tcp { get; set; }

        /// <summary>
        /// HTTP配置
        /// </summary>
        public HttpConfig Http { get; set; }

        /// <summary>
        /// 串口配置
        /// </summary>
        public SerialConfig Serial { get; set; }

        /// <summary>多个底层连接；键就是驱动代码中使用的连接名。</summary>
        public Dictionary<string, ConnectionConfig> Connections { get; set; } = new();

        /// <summary>
        /// 底层设备/DLL调用并发数；不影响上位系统的业务并行能力。
        /// </summary>
        public int DeviceCallResources { get; set; } = 1;

        /// <summary>
        /// 等待底层设备调用资源超时（毫秒）。
        /// </summary>
        public int DeviceCallTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 驱动自定义配置。建议在YAML中放到 device.settings 下。
        /// device 下未被SDK识别的字段也会自动合并到这里。
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        [YamlIgnore]
        public Dictionary<string, object> AdditionalSettings { get; set; } = new();
    }

    /// <summary>
    /// TCP配置
    /// </summary>
    public class TcpConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public int TimeoutMs { get; set; } = 5000;
        public int? ConnectTimeoutMs { get; set; }
        public int? ReadTimeoutMs { get; set; }
        public int? WriteTimeoutMs { get; set; }
    }

    /// <summary>
    /// HTTP配置
    /// </summary>
    public class HttpConfig
    {
        public string BaseUrl { get; set; }
        public int TimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    /// 串口配置
    /// </summary>
    public class SerialConfig
    {
        public string Port { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public double StopBits { get; set; } = 1;
        public string Parity { get; set; } = "none";
        public int TimeoutMs { get; set; } = 5000;
        public int? ReadTimeoutMs { get; set; }
        public int? WriteTimeoutMs { get; set; }
        public string Encoding { get; set; } = "utf-8";
        public string FlowControl { get; set; } = "none";
        public bool DtrEnable { get; set; }
        public bool RtsEnable { get; set; }
        public bool DiscardInputBeforeWrite { get; set; }
    }

    /// <summary>命名连接的声明式配置。</summary>
    public class ConnectionConfig
    {
        public string Type { get; set; }
        public bool Default { get; set; }
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
        public int MaxConcurrency { get; set; } = 1;
        public string ResourceGroup { get; set; }
        public int RetryCount { get; set; }
        public int RetryDelayMs { get; set; } = 200;
        public double RetryBackoff { get; set; } = 2;
        public string Executable { get; set; }
        public List<string> Arguments { get; set; } = new();
        public string WorkingDirectory { get; set; }
        public string Architecture { get; set; } = "auto";
        public string Framework { get; set; }
        public string ApartmentState { get; set; } = "MTA";
        public int ShutdownTimeoutMs { get; set; } = 5000;
        public Dictionary<string, string> Environment { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
    }
}

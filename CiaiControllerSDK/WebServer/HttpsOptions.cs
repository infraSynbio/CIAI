using System;

namespace CiaiControllerSDK.WebServer
{
    /// <summary>
    /// 服务器配置选项（支持HTTP和HTTPS）
    /// 参照 IncubatorController 的 Spring Boot SSL 配置
    /// </summary>
    public class HttpsOptions
    {
        /// <summary>
        /// 监听端口，默认443（HTTPS）或8080（HTTP）
        /// </summary>
        public int Port { get; set; } = 443;

        /// <summary>
        /// 是否启用HTTPS，默认true
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// 服务端证书文件路径（.pfx/.p12格式），HTTPS模式必需
        /// 对应 Spring Boot: server.ssl.key-store
        /// </summary>
        public string ServerCertificatePath { get; set; }

        /// <summary>
        /// 服务端证书密码
        /// 对应 Spring Boot: server.ssl.key-store-password
        /// </summary>
        public string ServerCertificatePassword { get; set; }

        /// <summary>
        /// 密钥别名
        /// 对应 Spring Boot: server.ssl.key-alias
        /// </summary>
        public string KeyAlias { get; set; }

        /// <summary>
        /// 密钥库类型，默认 PKCS12
        /// 对应 Spring Boot: server.ssl.key-store-type
        /// </summary>
        public string KeyStoreType { get; set; } = "PKCS12";

        /// <summary>
        /// 信任库文件路径，用于客户端证书验证
        /// 对应 Spring Boot: server.ssl.trust-store
        /// 如果不配置，将使用密钥库作为信任库（与 IncubatorController 一致）
        /// </summary>
        public string TrustStorePath { get; set; }

        /// <summary>
        /// 信任库密码
        /// 对应 Spring Boot: server.ssl.trust-store-password
        /// </summary>
        public string TrustStorePassword { get; set; }

        /// <summary>
        /// 信任库类型，默认 PKCS12
        /// </summary>
        public string TrustStoreType { get; set; } = "PKCS12";

        /// <summary>
        /// SSL/TLS 协议版本，默认 TLSv1.2（兼容 Java 8 基线）
        /// 对应 Spring Boot: server.ssl.protocol
        /// </summary>
        public string Protocol { get; set; } = "TLSv1.2";

        /// <summary>
        /// 启用的协议列表
        /// 对应 Spring Boot: server.ssl.enabled-protocols
        /// </summary>
        public string[] EnabledProtocols { get; set; } = new[] { "TLSv1.2" };

        /// <summary>
        /// 加密套件列表
        /// 对应 Spring Boot: server.ssl.ciphers
        /// </summary>
        public string[] Ciphers { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 客户端认证模式
        /// 对应 Spring Boot: server.ssl.client-auth
        /// </summary>
        public ClientAuthMode ClientAuth { get; set; } = ClientAuthMode.Need;

        /// <summary>
        /// 是否要求客户端证书验证，默认true（仅HTTPS模式有效）
        /// </summary>
        public bool RequireClientCertificate { get; set; } = true;

        /// <summary>
        /// 受信任的客户端证书指纹列表
        /// </summary>
        public string[] TrustedClientThumbprints { get; set; }

        /// <summary>
        /// 受信任的CA证书指纹列表
        /// </summary>
        public string[] TrustedIssuerThumbprints { get; set; }

        /// <summary>
        /// 回调URL地址
        /// </summary>
        public string CallbackUrl { get; set; }

        /// <summary>
        /// 回调超时时间（毫秒），默认30000
        /// </summary>
        public int CallbackTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 是否启用回调，默认true（当CallbackUrl不为空时）
        /// </summary>
        public bool EnableCallback { get; set; } = true;

        /// <summary>
        /// 主机名，默认localhost
        /// </summary>
        public string Host { get; set; } = "localhost";

        public int MaxConcurrentRequests { get; set; } = 100;
        public int MaxRequestBodyBytes { get; set; } = 1024 * 1024;
        public int FunctionQueueCapacity { get; set; } = 100;
        public int IdempotencyCapacity { get; set; } = 10000;
        public int ShutdownTimeoutMs { get; set; } = 30000;

        /// <summary>允许SDK替换端口上已有的不同HTTPS证书绑定；默认禁止破坏性替换。</summary>
        public bool AllowReplaceCertificateBinding { get; set; }

        /// <summary>
        /// 获取完整的监听前缀（根据UseHttps返回http或https）
        /// </summary>
        public string GetListenPrefix()
        {
            var scheme = UseHttps ? "https" : "http";
            var listenerHost = Host?.Trim();
            if (string.IsNullOrWhiteSpace(listenerHost) || listenerHost == "0.0.0.0" ||
                listenerHost == "*" || listenerHost == "+")
            {
                listenerHost = "+";
            }

            return $"{scheme}://{listenerHost}:{Port}/";
        }

        /// <summary>
        /// 获取localhost监听前缀（备用，权限不足时使用）
        /// </summary>
        public string GetLocalhostPrefix()
        {
            var scheme = UseHttps ? "https" : "http";
            return $"{scheme}://localhost:{Port}/";
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public void Validate()
        {
            if (Port <= 0 || Port > 65535)
                throw new ArgumentException($"无效的端口号: {Port}");

            if (string.IsNullOrWhiteSpace(Host))
                throw new ArgumentException("监听主机名不能为空");
            if (MaxConcurrentRequests <= 0 || MaxRequestBodyBytes <= 0 ||
                FunctionQueueCapacity <= 0 || IdempotencyCapacity <= 0 || ShutdownTimeoutMs <= 0)
                throw new ArgumentException("服务器并发、请求体、队列、幂等记录和停机超时配置必须大于0");

            // HTTPS模式需要证书
            if (UseHttps)
            {
                if (string.IsNullOrEmpty(ServerCertificatePath))
                    throw new ArgumentException("HTTPS模式需要配置服务端证书路径(ServerCertificatePath)");

                if (!System.IO.File.Exists(ServerCertificatePath))
                    throw new ArgumentException($"服务端证书文件不存在: {ServerCertificatePath}");

                // 客户端证书验证配置检查
                // 如果 ClientAuth 为 Need 或 Want，则需要配置信任
                if (ClientAuth == ClientAuthMode.Need || ClientAuth == ClientAuthMode.Want)
                {
                    // 信任库路径可以为空，此时使用密钥库作为信任库（与 IncubatorController 一致）
                    // 如果配置了信任库路径，则验证文件是否存在
                    if (!string.IsNullOrEmpty(TrustStorePath) && !System.IO.File.Exists(TrustStorePath))
                    {
                        throw new ArgumentException($"信任库文件不存在: {TrustStorePath}");
                    }
                }
            }
        }

        /// <summary>
        /// 创建HTTP模式配置（无需证书）
        /// </summary>
        public static HttpsOptions CreateHttp(int port = 8080, string host = "localhost")
        {
            return new HttpsOptions
            {
                UseHttps = false,
                Port = port,
                Host = host,
                RequireClientCertificate = false,
                ClientAuth = ClientAuthMode.None
            };
        }

        /// <summary>
        /// 创建HTTPS模式配置
        /// </summary>
        public static HttpsOptions CreateHttps(int port = 443, string host = "localhost",
            string certPath = null, string certPassword = null)
        {
            return new HttpsOptions
            {
                UseHttps = true,
                Port = port,
                Host = host,
                ServerCertificatePath = certPath,
                ServerCertificatePassword = certPassword
            };
        }

        /// <summary>
        /// 创建完整的HTTPS配置（参照 IncubatorController 的 Spring Boot 配置）
        /// </summary>
        /// <param name="port">端口</param>
        /// <param name="host">主机</param>
        /// <param name="keyStorePath">密钥库路径 (server.ssl.key-store)</param>
        /// <param name="keyStorePassword">密钥库密码 (server.ssl.key-store-password)</param>
        /// <param name="keyStoreType">密钥库类型 (server.ssl.key-store-type)</param>
        /// <param name="keyAlias">密钥别名 (server.ssl.key-alias)</param>
        /// <param name="trustStorePath">信任库路径 (server.ssl.trust-store)，可为null则使用密钥库</param>
        /// <param name="trustStorePassword">信任库密码 (server.ssl.trust-store-password)</param>
        /// <param name="protocol">SSL协议 (server.ssl.protocol)</param>
        /// <param name="ciphers">加密套件 (server.ssl.ciphers)</param>
        /// <param name="clientAuth">客户端认证模式 (server.ssl.client-auth)</param>
        public static HttpsOptions CreateHttpsFull(
            int port, string host,
            string keyStorePath, string keyStorePassword, string keyStoreType, string keyAlias,
            string trustStorePath, string trustStorePassword,
            string protocol, string[] ciphers,
            ClientAuthMode clientAuth)
        {
            return new HttpsOptions
            {
                Port = port,
                Host = host,
                UseHttps = true,

                // 密钥库配置
                ServerCertificatePath = keyStorePath,
                ServerCertificatePassword = keyStorePassword,
                KeyStoreType = keyStoreType ?? "PKCS12",
                KeyAlias = keyAlias,

                // 信任库配置
                TrustStorePath = trustStorePath,
                TrustStorePassword = trustStorePassword,

                // TLS配置
                Protocol = protocol ?? "TLSv1.2",
                EnabledProtocols = new[] { protocol ?? "TLSv1.2" },
                Ciphers = ciphers ?? Array.Empty<string>(),

                // 客户端认证
                ClientAuth = clientAuth,
                RequireClientCertificate = clientAuth != ClientAuthMode.None
            };
        }
    }

    /// <summary>
    /// 客户端认证模式
    /// 对应 Spring Boot: server.ssl.client-auth
    /// </summary>
    public enum ClientAuthMode
    {
        /// <summary>
        /// 需要客户端证书
        /// </summary>
        Need,

        /// <summary>
        /// 想要客户端证书，但不是必须
        /// </summary>
        Want,

        /// <summary>
        /// 不需要客户端证书
        /// </summary>
        None
    }
}

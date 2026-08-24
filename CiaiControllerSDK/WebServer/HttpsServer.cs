using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Logging;

namespace CiaiControllerSDK.WebServer
{
    /// <summary>
    /// HTTP/HTTPS服务器 - 支持HTTPS双向认证(mTLS)
    /// </summary>
    public class HttpsServer : IDisposable, IAsyncDisposable
    {
        private readonly HttpsOptions _options;
        private readonly RequestHandler _handler;
        private readonly ILogger _logger;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private X509Certificate2Collection _trustedCertificates;
        private bool _disposed;
        private bool _isRunning;
        private readonly SemaphoreSlim _requestSlots;
        private readonly ConcurrentDictionary<int, Task> _activeRequests = new();
        private Task _acceptLoop;
        private int _requestId;

        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port => _options.Port;

        /// <summary>
        /// 是否使用HTTPS
        /// </summary>
        public bool UseHttps => _options.UseHttps;

        /// <summary>
        /// 创建HTTP/HTTPS服务器
        /// </summary>
        /// <param name="options">服务器配置选项</param>
        /// <param name="handler">请求处理器</param>
        public HttpsServer(HttpsOptions options, RequestHandler handler)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = LoggerProvider.CreateLogger<HttpsServer>();
            _requestSlots = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
                throw new InvalidOperationException("服务器已在运行");

            // 验证配置
            _options.Validate();

            // HTTPS模式需要绑定SSL证书
            if (_options.UseHttps)
            {
                await BindCertificateToPortAsync();
                if (_options.ClientAuth != ClientAuthMode.None)
                {
                    _trustedCertificates = LoadTrustCertificates();
                }
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener = new HttpListener();

            var scheme = _options.UseHttps ? "HTTPS" : "HTTP";
            var prefix = _options.GetListenPrefix(); // 使用 * 监听所有接口

            _listener.Prefixes.Add(prefix);

            try
            {
                _listener.Start();
                _isRunning = true;
                _logger.LogInformation("{Scheme}服务器启动，监听: {Prefix}", scheme, prefix);

                // 开始接收请求
                _acceptLoop = ProcessRequestsAsync(_cts.Token);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5) // 拒绝访问，需要URL ACL
            {
                _logger.LogWarning("权限不足，尝试使用localhost监听...");

                // 尝试使用localhost监听
                _listener.Close();
                _listener = new HttpListener();
                var localhostPrefix = _options.GetLocalhostPrefix();
                _listener.Prefixes.Add(localhostPrefix);

                try
                {
                    _listener.Start();
                    _isRunning = true;
                    _logger.LogInformation("{Scheme}服务器启动，监听: {Prefix}", scheme, localhostPrefix);
                    _logger.LogWarning("提示: 仅监听localhost，外部无法访问。如需监听所有接口，请以管理员身份运行。");

                    // 开始接收请求
                    _acceptLoop = ProcessRequestsAsync(_cts.Token);
                }
                catch (Exception innerEx)
                {
                    throw new InvalidOperationException(
                        $"无法启动服务器: {innerEx.Message}\n\n" +
                        $"解决方案（二选一）：\n" +
                        $"1. 以管理员身份运行程序\n" +
                        $"2. 手动执行以下命令（需管理员权限，只需执行一次）：\n" +
                        $"   netsh http add urlacl url={(_options.UseHttps ? "https" : "http")}://*:{_options.Port}/ user=\"Everyone\"");
                }
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 183) // 端口被占用
            {
                throw new InvalidOperationException($"端口 {_options.Port} 已被占用，请更换端口或关闭占用该端口的程序");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动失败");
                throw;
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cts?.Cancel();

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }

            if (_acceptLoop != null)
            {
                try { await _acceptLoop; }
                catch (OperationCanceledException) { }
            }

            var active = Task.WhenAll(_activeRequests.Values.ToArray());
            var completed = await Task.WhenAny(active, Task.Delay(_options.ShutdownTimeoutMs));
            if (completed != active)
                _logger.LogWarning("服务器停机等待超时，仍有 {Count} 个请求在执行", _activeRequests.Count);

            _logger.LogInformation("服务器已停止");
        }

        /// <summary>
        /// 处理请求循环
        /// </summary>
        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                var acquired = false;
                try
                {
                    await _requestSlots.WaitAsync(cancellationToken);
                    acquired = true;
                    var context = await _listener.GetContextAsync();
                    var id = Interlocked.Increment(ref _requestId);
                    var task = ProcessTrackedRequestAsync(id, context, cancellationToken);
                    acquired = false;
                    _activeRequests[id] = task;
                    if (task.IsCompleted)
                        _activeRequests.TryRemove(id, out _);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    if (acquired)
                        _requestSlots.Release();
                    // 正常关闭
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || !_isRunning)
                {
                    if (acquired)
                        _requestSlots.Release();
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (acquired)
                        _requestSlots.Release();
                    break;
                }
                catch (Exception ex)
                {
                    if (acquired)
                        _requestSlots.Release();
                    _logger.LogError(ex, "接收请求异常");
                }
            }
        }

        private async Task ProcessTrackedRequestAsync(int id, HttpListenerContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                await ProcessSingleRequestAsync(context, cancellationToken);
            }
            finally
            {
                _activeRequests.TryRemove(id, out _);
                _requestSlots.Release();
            }
        }

        /// <summary>
        /// 处理单个请求
        /// </summary>
        private async Task ProcessSingleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // HTTPS模式下验证客户端证书（参照 IncubatorController: server.ssl.client-auth）
                if (_options.UseHttps && _options.ClientAuth != ClientAuthMode.None)
                {
                    var clientCert = request.GetClientCertificate();

                    if (clientCert == null)
                    {
                        // Need 模式下必须要有客户端证书
                        if (_options.ClientAuth == ClientAuthMode.Need)
                        {
                            await SendResponseAsync(response, HttpResponse.Forbidden("需要客户端证书"));
                            return;
                        }
                        // Want 模式下可以没有客户端证书，继续处理请求
                    }
                    else if (!ValidateClientCertificate(clientCert))
                    {
                        await SendResponseAsync(response, HttpResponse.Forbidden("客户端证书验证失败"));
                        return;
                    }
                }

                // 读取请求体
                string body = null;
                if (request.HasEntityBody)
                {
                    if (request.ContentLength64 > _options.MaxRequestBodyBytes)
                    {
                        await SendResponseAsync(response, HttpResponse.PayloadTooLarge());
                        return;
                    }
                    body = await ReadRequestBodyAsync(request, cancellationToken);
                }

                // 处理请求
                var httpResponse = await _handler.HandleRequestAsync(
                    request.Url?.AbsolutePath ?? "/",
                    request.HttpMethod,
                    body);

                await SendResponseAsync(response, httpResponse);
            }
            catch (RequestBodyTooLargeException)
            {
                await SendResponseAsync(response, HttpResponse.PayloadTooLarge());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理请求异常");
                await SendResponseAsync(response, HttpResponse.InternalError("服务器内部错误"));
            }
        }

        private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request,
            CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = await request.InputStream.ReadAsync(
                    buffer, 0, buffer.Length, cancellationToken);
                if (count == 0)
                    break;
                if (stream.Length + count > _options.MaxRequestBodyBytes)
                    throw new RequestBodyTooLargeException();
                await stream.WriteAsync(buffer, 0, count, cancellationToken);
            }
            return (request.ContentEncoding ?? Encoding.UTF8).GetString(stream.ToArray());
        }

        /// <summary>
        /// 验证客户端证书
        /// </summary>
        private bool ValidateClientCertificate(X509Certificate2 certificate)
        {
            if (certificate == null)
                return false;

            var thumbprint = NormalizeThumbprint(certificate.Thumbprint);

            // 检查是否在受信任的客户端证书列表中
            if (_options.TrustedClientThumbprints != null && _options.TrustedClientThumbprints.Length > 0)
            {
                foreach (var trusted in _options.TrustedClientThumbprints)
                {
                    if (string.Equals(thumbprint, NormalizeThumbprint(trusted), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Java SDK使用trustStore构建客户端证书信任链。若信任库中直接
            // 包含客户端叶证书，也将其视为明确受信任证书。
            if (_trustedCertificates != null)
            {
                foreach (var trustedCertificate in _trustedCertificates)
                {
                    if (string.Equals(thumbprint, NormalizeThumbprint(trustedCertificate.Thumbprint),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            if (_trustedCertificates != null && _trustedCertificates.Count > 0)
            {
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                foreach (var trustedCertificate in _trustedCertificates)
                {
                    chain.ChainPolicy.ExtraStore.Add(trustedCertificate);
                    chain.ChainPolicy.CustomTrustStore.Add(trustedCertificate);
                }
            }

            var chainValid = chain.Build(certificate);

            if (_options.TrustedIssuerThumbprints != null && _options.TrustedIssuerThumbprints.Length > 0)
            {
                foreach (var chainElement in chain.ChainElements)
                {
                    var elementThumbprint = NormalizeThumbprint(chainElement.Certificate?.Thumbprint);
                    if (_options.TrustedIssuerThumbprints.Any(trustedIssuer =>
                        string.Equals(elementThumbprint, NormalizeThumbprint(trustedIssuer),
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            return chainValid;
        }

        private X509Certificate2Collection LoadTrustCertificates()
        {
            var trustStorePath = string.IsNullOrWhiteSpace(_options.TrustStorePath)
                ? _options.ServerCertificatePath
                : _options.TrustStorePath;
            var trustStorePassword = string.IsNullOrWhiteSpace(_options.TrustStorePath)
                ? _options.ServerCertificatePassword
                : _options.TrustStorePassword;

            var certificates = new X509Certificate2Collection();
            certificates.Import(
                trustStorePath,
                trustStorePassword,
                X509KeyStorageFlags.EphemeralKeySet);

            _logger.LogInformation("已从信任库加载 {Count} 个证书: {Path}", certificates.Count, trustStorePath);
            return certificates;
        }

        private static string NormalizeThumbprint(string thumbprint)
        {
            return string.IsNullOrWhiteSpace(thumbprint)
                ? string.Empty
                : new string(thumbprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        }

        /// <summary>
        /// 发送响应
        /// </summary>
        private static async Task SendResponseAsync(HttpListenerResponse response, HttpResponse httpResponse)
        {
            response.StatusCode = httpResponse.StatusCode;
            response.ContentType = httpResponse.ContentType;

            var buffer = Encoding.UTF8.GetBytes(httpResponse.Body ?? string.Empty);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// 绑定SSL证书到端口（仅HTTPS模式）
        /// </summary>
        private async Task BindCertificateToPortAsync()
        {
            // 加载证书并导入到LocalMachine\My存储（netsh http add sslcert要求证书在存储中）
            var certificate = new X509Certificate2(
                _options.ServerCertificatePath,
                _options.ServerCertificatePassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            // 将证书导入到LocalMachine\My存储，确保证书私钥对系统可用
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                var existing = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                if (existing.Count == 0)
                {
                    store.Add(certificate);
                    _logger.LogInformation("已将证书导入到 LocalMachine\\My 存储，Thumbprint: {Thumbprint}", certificate.Thumbprint);
                }
                else
                {
                    _logger.LogInformation("证书已存在于 LocalMachine\\My 存储，Thumbprint: {Thumbprint}", certificate.Thumbprint);
                }
                store.Close();
            }

            var certHash = certificate.Thumbprint;
            var appId = Guid.NewGuid().ToString();

            // 检查是否已绑定
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"http show sslcert ipport=0.0.0.0:{_options.Port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            checkProcess.Start();
            var output = await checkProcess.StandardOutput.ReadToEndAsync();
            checkProcess.WaitForExit();

            // 如果已绑定且指纹相同，跳过
            if (output.Contains(certHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("SSL证书已绑定到端口 {Port}", _options.Port);
                return;
            }

            // 如果已绑定但指纹不同，先删除
            if (output.Contains("IP:port"))
            {
                if (!_options.AllowReplaceCertificateBinding)
                {
                    throw new InvalidOperationException(
                        $"端口 {_options.Port} 已绑定其他SSL证书。SDK不会默认删除现有绑定；" +
                        "请手工确认并处理，或显式设置server.allowReplaceCertificateBinding=true");
                }
                var deleteProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"http delete sslcert ipport=0.0.0.0:{_options.Port}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                deleteProcess.Start();
                await deleteProcess.StandardOutput.ReadToEndAsync();
                deleteProcess.WaitForExit();
            }

            // 绑定新证书
            // 根据客户端认证模式决定是否启用客户端证书协商
            // 参照 IncubatorController: server.ssl.client-auth
            var clientCertNegotiation = (_options.ClientAuth == ClientAuthMode.Need || _options.ClientAuth == ClientAuthMode.Want)
                ? "enable"
                : "disable";

            _logger.LogInformation("绑定SSL证书到端口 {Port}，客户端证书协商: {ClientCertNegotiation}, TLS协议: {Protocol}",
                _options.Port, clientCertNegotiation, _options.Protocol);

            var bindProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"http add sslcert ipport=0.0.0.0:{_options.Port} certhash={certHash} appid={{{appId}}} clientcertnegotiation={clientCertNegotiation}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            bindProcess.Start();
            var bindOutput = await bindProcess.StandardOutput.ReadToEndAsync();
            var bindError = await bindProcess.StandardError.ReadToEndAsync();
            bindProcess.WaitForExit();

            if (bindProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"绑定SSL证书失败: {bindError}\n请确保以管理员权限运行，或手动执行以下命令:\nnetsh http add sslcert ipport=0.0.0.0:{_options.Port} certhash={certHash} appid={{{appId}}} clientcertnegotiation={clientCertNegotiation}");
            }

            _logger.LogInformation("SSL证书绑定成功，端口: {Port}", _options.Port);
        }

        /// <summary>
        /// 注册URL预留（需要管理员权限）
        /// </summary>
        public static void RegisterUrlAcl(int port, bool useHttps = true, string user = "Everyone")
        {
            var scheme = useHttps ? "https" : "http";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"http add urlacl url={scheme}://*:{port}/ user=\"{user}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"注册URL预留失败。请以管理员权限运行，或手动执行:\nnetsh http add urlacl url={scheme}://*:{port}/ user=\"{user}\"");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            _cts?.Dispose();
            _requestSlots.Dispose();
            if (_trustedCertificates != null)
            {
                foreach (var certificate in _trustedCertificates)
                {
                    certificate.Dispose();
                }
                _trustedCertificates = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await StopAsync();
            _cts?.Dispose();
            _requestSlots.Dispose();
            if (_trustedCertificates != null)
            {
                foreach (var certificate in _trustedCertificates)
                    certificate.Dispose();
                _trustedCertificates = null;
            }
        }

        private sealed class RequestBodyTooLargeException : Exception { }
    }
}

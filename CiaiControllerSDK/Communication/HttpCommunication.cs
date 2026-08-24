using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Interfaces;
using CiaiControllerSDK.Logging;

namespace CiaiControllerSDK.Communication
{
    /// <summary>
    /// HTTP通信实现
    /// </summary>
    public class HttpCommunication : ICommunication, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly int _timeout;
        private readonly ILogger _logger;
        private bool _disposed;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string BaseUrl => _baseUrl;
        public int Timeout => _timeout;

        public HttpCommunication(string baseUrl, int timeout = 30000,
            IDictionary<string, string> defaultHeaders = null)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("必须提供有效的HTTP/HTTPS基础地址", nameof(baseUrl));
            if (timeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeout), "HTTP超时必须大于0");
            _baseUrl = baseUrl.TrimEnd('/');
            _timeout = timeout;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeout)
            };
            ApplyDefaultHeaders(defaultHeaders);
            _logger = LoggerProvider.CreateLogger<HttpCommunication>();
        }

        public HttpCommunication(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("必须提供有效的HTTP/HTTPS基础地址", nameof(baseUrl));
            _baseUrl = baseUrl.TrimEnd('/');
            _timeout = (int)httpClient.Timeout.TotalMilliseconds;
            _logger = LoggerProvider.CreateLogger<HttpCommunication>();
        }

        private void ApplyDefaultHeaders(IDictionary<string, string> headers)
        {
            if (headers == null) return;
            foreach (var pair in headers)
            {
                if (!_httpClient.DefaultRequestHeaders.TryAddWithoutValidation(pair.Key, pair.Value))
                    throw new ArgumentException($"无效的HTTP默认请求头: {pair.Key}");
            }
        }

        /// <summary>执行任意HTTP方法，支持每次请求的头、内容类型和取消。</summary>
        public async Task<HttpResponseMessage> RequestAsync(HttpMethod method, string endpoint,
            HttpContent content = null, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            var url = string.IsNullOrWhiteSpace(endpoint) ? _baseUrl :
                $"{_baseUrl}/{endpoint.TrimStart('/')}";
            using var request = new HttpRequestMessage(method, url) { Content = content };
            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(pair.Key, pair.Value) &&
                        !(request.Content?.Headers.TryAddWithoutValidation(pair.Key, pair.Value) ?? false))
                        throw new ArgumentException($"无效的HTTP请求头: {pair.Key}");
                }
            }
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }

        public Task<HttpResponseMessage> PatchAsync(string endpoint, HttpContent content,
            CancellationToken cancellationToken = default) =>
            RequestAsync(new HttpMethod("PATCH"), endpoint, content, null, cancellationToken);

        public Task<bool> ConnectAsync()
        {
            // HTTP无状态；初始化只验证客户端配置，不对设备根路径发起有副作用或不被支持的探测请求。
            _isConnected = true;
            _logger.LogInformation("HTTP客户端已就绪: {BaseUrl}", _baseUrl);
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            if (_isConnected)
            {
                _logger.LogInformation("断开HTTP连接: {BaseUrl}", _baseUrl);
            }
            _isConnected = false;
            return Task.CompletedTask;
        }

        public async Task<bool> SendAsync(byte[] data)
        {
            try
            {
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                using var response = await _httpClient.PostAsync(_baseUrl, content);

                _logger.LogDebug("HTTP发送数据: {Length}字节, 状态码: {StatusCode}", data.Length, response.StatusCode);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP发送数据失败: {Length}字节", data.Length);
                return false;
            }
        }

        public async Task<byte[]> ReceiveAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(_baseUrl);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();
                    _logger.LogDebug("HTTP接收数据: {Length}字节", data.Length);
                    return data;
                }

                _logger.LogWarning("HTTP接收失败: 状态码 {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP接收数据失败");
                return null;
            }
        }

        public async Task<byte[]> SendAndReceiveAsync(byte[] data)
        {
            try
            {
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                using var response = await _httpClient.PostAsync(_baseUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsByteArrayAsync();
                    _logger.LogDebug("HTTP发送并接收: 发送{SendLength}字节, 接收{ReceiveLength}字节", data.Length, result.Length);
                    return result;
                }

                _logger.LogWarning("HTTP发送并接收失败: 状态码 {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP发送并接收失败");
                return null;
            }
        }

        /// <summary>
        /// GET请求
        /// </summary>
        public async Task<string> GetAsync(string endpoint)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            try
            {
                _logger.LogDebug("HTTP GET请求: {Url}", url);
                using var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("HTTP GET响应: {Length}字符", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP GET请求失败: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// POST请求
        /// </summary>
        public async Task<string> PostAsync(string endpoint, string jsonContent)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            try
            {
                _logger.LogDebug("HTTP POST请求: {Url}, 内容长度: {Length}字符", url, jsonContent.Length);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("HTTP POST响应: {Length}字符", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP POST请求失败: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// POST请求（返回原始响应）
        /// </summary>
        public async Task<HttpResponseMessage> PostRawAsync(string endpoint, string jsonContent)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            try
            {
                _logger.LogDebug("HTTP POST原始请求: {Url}", url);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                _logger.LogDebug("HTTP POST原始响应: 状态码 {StatusCode}", response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP POST原始请求失败: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// PUT请求。
        /// </summary>
        public async Task<string> PutAsync(string endpoint, string jsonContent)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            try
            {
                _logger.LogDebug("HTTP PUT请求: {Url}, 内容长度: {Length}字符", url, jsonContent?.Length ?? 0);
                var content = new StringContent(jsonContent ?? string.Empty, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PutAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP PUT请求失败: {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// DELETE请求。
        /// </summary>
        public async Task<string> DeleteAsync(string endpoint)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            try
            {
                _logger.LogDebug("HTTP DELETE请求: {Url}", url);
                using var response = await _httpClient.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP DELETE请求失败: {Url}", url);
                return null;
            }
        }

        #region 同步方法

        /// <summary>
        /// 连接设备（同步）
        /// </summary>
        public bool Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 断开连接（同步）
        /// </summary>
        public void Disconnect()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 发送数据（同步）
        /// </summary>
        public bool Send(byte[] data)
        {
            return SendAsync(data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 接收数据（同步）
        /// </summary>
        public byte[] Receive()
        {
            return ReceiveAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 发送并接收（同步）
        /// </summary>
        public byte[] SendAndReceive(byte[] data)
        {
            return SendAndReceiveAsync(data).GetAwaiter().GetResult();
        }

        /// <summary>
        /// GET请求（同步）
        /// </summary>
        public string Get(string endpoint)
        {
            return GetAsync(endpoint).GetAwaiter().GetResult();
        }

        /// <summary>
        /// POST请求（同步）
        /// </summary>
        public string Post(string endpoint, string jsonContent)
        {
            return PostAsync(endpoint, jsonContent).GetAwaiter().GetResult();
        }

        /// <summary>
        /// POST请求（同步，返回原始响应）
        /// </summary>
        public HttpResponseMessage PostRaw(string endpoint, string jsonContent)
        {
            return PostRawAsync(endpoint, jsonContent).GetAwaiter().GetResult();
        }

        public string Put(string endpoint, string jsonContent)
        {
            return PutAsync(endpoint, jsonContent).GetAwaiter().GetResult();
        }

        public string Delete(string endpoint)
        {
            return DeleteAsync(endpoint).GetAwaiter().GetResult();
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _httpClient?.Dispose();
            }
        }
    }
}

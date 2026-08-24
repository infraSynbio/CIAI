using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Logging;
using CiaiControllerSDK.Models;

namespace CiaiControllerSDK.Callback
{
    /// <summary>
    /// HTTP回调工具类
    /// </summary>
    public class HttpCallback : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _callbackUrl;
        private readonly bool _enabled;
        private readonly ILogger _logger;
        private bool _disposed;

        /// <summary>
        /// 创建HTTP回调实例
        /// </summary>
        /// <param name="callbackUrl">回调URL</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="enabled">是否启用</param>
        public HttpCallback(string callbackUrl, int timeoutMs = 30000, bool enabled = true)
        {
            _callbackUrl = callbackUrl;
            _enabled = enabled && !string.IsNullOrEmpty(callbackUrl);
            _httpClient = new HttpClient();
            _httpClient.Timeout = timeoutMs > 0
                ? TimeSpan.FromMilliseconds(timeoutMs)
                : System.Threading.Timeout.InfiniteTimeSpan;
            _logger = LoggerProvider.CreateLogger<HttpCallback>();
        }

        /// <summary>
        /// 是否启用回调
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 发送Function完成回调
        /// </summary>
        /// <param name="finish">完成结果</param>
        public async Task<bool> PostFinishAsync(Finish finish)
        {
            if (!_enabled)
                return true;

            try
            {
                var json = JsonSerializer.Serialize(finish, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(_callbackUrl, content);
                var success = response.IsSuccessStatusCode;
                if (!success)
                    _logger.LogError("回调失败，HTTP状态码: {StatusCode}", response.StatusCode);
                return success;
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，避免影响主流程
                _logger.LogError(ex, "回调失败");
                return false;
            }
        }

        /// <summary>
        /// 发送自定义JSON回调
        /// </summary>
        /// <param name="data">要发送的数据对象</param>
        public async Task<bool> PostAsync<T>(T data)
        {
            if (!_enabled)
                return true;

            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(_callbackUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "回调失败");
                return false;
            }
        }

        /// <summary>
        /// 发送原始JSON字符串回调
        /// </summary>
        /// <param name="jsonContent">JSON内容</param>
        public async Task<bool> PostRawAsync(string jsonContent)
        {
            if (!_enabled)
                return true;

            try
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(_callbackUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "回调失败");
                return false;
            }
        }

        /// <summary>
        /// 同步发送原始JSON字符串回调。
        /// </summary>
        public bool PostRaw(string jsonContent)
        {
            return PostRawAsync(jsonContent).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 创建空的回调实例（禁用状态）
        /// </summary>
        public static HttpCallback CreateDisabled()
        {
            return new HttpCallback(null, 0, false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _httpClient?.Dispose();
        }
    }
}

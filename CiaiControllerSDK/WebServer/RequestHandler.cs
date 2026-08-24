using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Callback;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Logging;
using CiaiControllerSDK.Models;

namespace CiaiControllerSDK.WebServer
{
    /// <summary>
    /// 请求处理器 - 处理HTTP请求并调用驱动方法
    /// </summary>
    public class RequestHandler : IDisposable, IAsyncDisposable
    {
        private readonly DeviceDriverBase _driver;
        private readonly HttpCallback _callback;
        private readonly bool _enableCallback;
        private readonly ILogger _logger;
        private readonly Channel<FunctionData> _functionQueue;
        private readonly Task[] _functionWorkers;
        private readonly ConcurrentDictionary<string, byte> _acceptedInstructions = new();
        private readonly ConcurrentQueue<string> _instructionOrder = new();
        private readonly int _idempotencyCapacity;
        private readonly TimeSpan _shutdownTimeout;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            WriteIndented = false
        };

        /// <summary>
        /// 创建请求处理器
        /// </summary>
        /// <param name="driver">驱动实例</param>
        /// <param name="callbackUrl">回调URL（可选）</param>
        /// <param name="callbackTimeoutMs">回调超时时间</param>
        /// <param name="enableCallback">是否启用回调</param>
        /// <param name="functionQueueCapacity">函数调用等待队列容量</param>
        /// <param name="idempotencyCapacity">幂等结果缓存容量</param>
        /// <param name="shutdownTimeoutMs">关闭时等待任务完成的超时时间</param>
        public RequestHandler(DeviceDriverBase driver, string callbackUrl = null, int callbackTimeoutMs = 30000,
            bool enableCallback = true, int functionQueueCapacity = 100,
            int idempotencyCapacity = 10000, int shutdownTimeoutMs = 30000)
        {
            if (functionQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(functionQueueCapacity));
            if (idempotencyCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(idempotencyCapacity));
            if (shutdownTimeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeoutMs));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _enableCallback = enableCallback && !string.IsNullOrEmpty(callbackUrl);
            _callback = _enableCallback
                ? new HttpCallback(callbackUrl, callbackTimeoutMs, enabled: true)
                : HttpCallback.CreateDisabled();
            _logger = LoggerProvider.CreateLogger<RequestHandler>();
            _idempotencyCapacity = idempotencyCapacity;
            _shutdownTimeout = TimeSpan.FromMilliseconds(shutdownTimeoutMs);
            _functionQueue = Channel.CreateBounded<FunctionData>(new BoundedChannelOptions(functionQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = driver.DriverAttribute.FunctionalResources == 1
            });
            _functionWorkers = new Task[driver.DriverAttribute.FunctionalResources];
            for (var i = 0; i < _functionWorkers.Length; i++)
                _functionWorkers[i] = Task.Run(FunctionWorkerAsync);
        }

        /// <summary>
        /// 处理请求
        /// </summary>
        /// <param name="path">请求路径</param>
        /// <param name="method">HTTP方法</param>
        /// <param name="body">请求体（可选）</param>
        /// <returns>响应内容</returns>
        public async Task<HttpResponse> HandleRequestAsync(string path, string method, string body = null)
        {
            var startTime = DateTime.Now;
            _logger.LogDebug("收到请求: {Method} {Path}", method, path);

            try
            {
                // 标准化路径
                path = (path ?? "/").TrimEnd('/');
                if (path.Length == 0)
                    path = "/";

                HttpResponse response = path switch
                {
                    RouteBuilder.Endpoints.Info when method == "GET" => await HandleInfoAsync(),
                    RouteBuilder.Endpoints.HeartBeat when method == "GET" => await HandleHeartBeatAsync(),
                    RouteBuilder.Endpoints.Function when method == "POST" => await HandleFunctionAsync(body),
                    RouteBuilder.Endpoints.Operation when method == "POST" => await HandleOperationAsync(body),
                    RouteBuilder.Endpoints.Set when method == "POST" => await HandleSetAsync(body),
                    RouteBuilder.Endpoints.Get when method == "GET" => await HandleGetAsync(),
                    RouteBuilder.Endpoints.EnterAndExit when method == "POST" => await HandleEnterAndExitAsync(body),
                    _ when RouteBuilder.Endpoints.IsKnown(path) => HttpResponse.MethodNotAllowed(),
                    _ => HttpResponse.NotFound($"未找到路由: {method} {path}")
                };

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                _logger.LogDebug("请求完成: {Method} {Path}, 状态码: {StatusCode}, 耗时: {Elapsed}ms",
                    method, path, response.StatusCode, elapsed);

                return response;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                _logger.LogError(ex, "请求处理异常: {Method} {Path}, 耗时: {Elapsed}ms", method, path, elapsed);
                return HttpResponse.InternalError("服务器内部错误");
            }
        }

        /// <summary>
        /// 处理Info请求
        /// </summary>
        private Task<HttpResponse> HandleInfoAsync()
        {
            _logger.LogDebug("获取设备信息");
            var result = _driver.GetRegisterInfo();
            return Task.FromResult(JsonResponse(result));
        }

        /// <summary>
        /// 处理HeartBeat请求
        /// </summary>
        private Task<HttpResponse> HandleHeartBeatAsync()
        {
            _logger.LogDebug("获取心跳状态");
            var result = _driver.GetHeartBeat();
            return Task.FromResult(JsonResponse(result));
        }

        /// <summary>
        /// 处理Function请求（异步执行，立即返回，完成后回调）
        /// </summary>
        private Task<HttpResponse> HandleFunctionAsync(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("Function请求失败: 请求体为空");
                return Task.FromResult(HttpResponse.BadRequest("请求体不能为空"));
            }

            FunctionData data;
            try
            {
                data = JsonSerializer.Deserialize<FunctionData>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Function请求失败: JSON解析失败");
                return Task.FromResult(HttpResponse.BadRequest("JSON格式错误"));
            }

            if (string.IsNullOrEmpty(data?.FunctionName))
            {
                _logger.LogWarning("Function请求失败: functionName为空");
                return Task.FromResult(HttpResponse.BadRequest("functionName不能为空"));
            }

            _logger.LogInformation("收到Function请求: {FunctionName}, 指令ID: {InstructionId}",
                data.FunctionName, data.InstructionId);

            var acceptResponse = Result<string>.Success("Function accepted");

            var hasInstructionId = !string.IsNullOrWhiteSpace(data.InstructionId);
            if (hasInstructionId && !_acceptedInstructions.TryAdd(data.InstructionId, 0))
                return Task.FromResult(JsonResponse(Result<string>.Success("Function already accepted")));

            if (!_functionQueue.Writer.TryWrite(data))
            {
                if (hasInstructionId)
                    _acceptedInstructions.TryRemove(data.InstructionId, out _);
                return Task.FromResult(HttpResponse.TooManyRequests("Function队列已满，请稍后重试"));
            }

            if (hasInstructionId)
            {
                _instructionOrder.Enqueue(data.InstructionId);
                TrimInstructionHistory();
            }

            return Task.FromResult(JsonResponse(acceptResponse));
        }

        private async Task FunctionWorkerAsync()
        {
            await foreach (var data in _functionQueue.Reader.ReadAllAsync())
            {
                try
                {
                    var result = await _driver.ExecuteFunctionAsync(data);
                    if (result.IsSuccess && result.Data != null)
                    {
                        result.Data.InstructionId ??= data.InstructionId;
                        result.Data.NestId ??= data.NestId;
                    }

                    if (_enableCallback && result.IsSuccess && result.Data != null)
                    {
                        await _callback.PostFinishAsync(result.Data);
                    }
                    else if (_enableCallback && !result.IsSuccess)
                    {
                        await _callback.PostFinishAsync(new Finish
                        {
                            Completion = "error",
                            ErrorMsg = result.Message,
                            InstructionId = data.InstructionId,
                            NestId = data.NestId
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Function执行异常: {FunctionName}", data.FunctionName);
                    if (_enableCallback)
                    {
                        await _callback.PostFinishAsync(new Finish
                        {
                            Completion = "error",
                            ErrorMsg = ex.Message,
                            InstructionId = data.InstructionId,
                            NestId = data.NestId
                        });
                    }
                }
            }
        }

        private void TrimInstructionHistory()
        {
            while (_acceptedInstructions.Count > _idempotencyCapacity &&
                   _instructionOrder.TryDequeue(out var oldest))
                _acceptedInstructions.TryRemove(oldest, out _);
        }

        /// <summary>
        /// 处理Operation请求（同步执行）
        /// </summary>
        private async Task<HttpResponse> HandleOperationAsync(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return HttpResponse.BadRequest("请求体不能为空");
            }

            OperationData data;
            try
            {
                data = JsonSerializer.Deserialize<OperationData>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return HttpResponse.BadRequest("JSON格式错误");
            }

            if (string.IsNullOrEmpty(data?.OperationName))
            {
                return HttpResponse.BadRequest("operationName不能为空");
            }

            var result = await _driver.ExecuteOperationAsync(data);
            return JsonResponse(result);
        }

        /// <summary>
        /// 处理Set请求
        /// </summary>
        private async Task<HttpResponse> HandleSetAsync(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return HttpResponse.BadRequest("请求体不能为空");
            }

            List<SetData> dataList;
            try
            {
                dataList = JsonSerializer.Deserialize<List<SetData>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return HttpResponse.BadRequest("JSON格式错误");
            }

            if (dataList == null || dataList.Count == 0)
            {
                return HttpResponse.BadRequest("设置参数列表不能为空");
            }

            var result = await _driver.ExecuteSetAsync(dataList);
            return JsonResponse(result);
        }

        /// <summary>
        /// 处理Get请求
        /// </summary>
        private Task<HttpResponse> HandleGetAsync()
        {
            var result = _driver.GetStatus();
            return Task.FromResult(JsonResponse(result));
        }

        /// <summary>
        /// 处理EnterAndExit请求
        /// </summary>
        private async Task<HttpResponse> HandleEnterAndExitAsync(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return HttpResponse.BadRequest("请求体不能为空");
            }

            EnterOrExitData data;
            try
            {
                data = JsonSerializer.Deserialize<EnterOrExitData>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return HttpResponse.BadRequest("JSON格式错误");
            }

            if (string.IsNullOrEmpty(data?.EnterOrExitName))
            {
                return HttpResponse.BadRequest("enterOrExitName不能为空");
            }

            var result = await _driver.ExecuteEnterExitAsync(data);
            return JsonResponse(result);
        }

        /// <summary>
        /// 创建JSON响应
        /// </summary>
        private static HttpResponse JsonResponse(object data)
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            return new HttpResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = json
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _functionQueue.Writer.TryComplete();
            var workers = Task.WhenAll(_functionWorkers);
            if (!workers.Wait(_shutdownTimeout))
                _logger.LogWarning("Function队列停机等待超时");
            _callback?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            _functionQueue.Writer.TryComplete();
            var workers = Task.WhenAll(_functionWorkers);
            if (await Task.WhenAny(workers, Task.Delay(_shutdownTimeout)) != workers)
                _logger.LogWarning("Function队列停机等待超时");
            _callback?.Dispose();
        }
    }

    /// <summary>
    /// HTTP响应
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 内容类型
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// 响应体
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 创建404响应
        /// </summary>
        public static HttpResponse NotFound(string message = "Not Found")
        {
            return new HttpResponse
            {
                StatusCode = 404,
                Body = JsonSerializer.Serialize(Result<object>.Failed(message))
            };
        }

        /// <summary>
        /// 创建400响应
        /// </summary>
        public static HttpResponse BadRequest(string message)
        {
            return new HttpResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(Result<object>.Failed(message))
            };
        }

        /// <summary>
        /// 创建500响应
        /// </summary>
        public static HttpResponse InternalError(string message)
        {
            return new HttpResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(Result<object>.ServerError(message))
            };
        }

        /// <summary>
        /// 创建403响应（客户端证书验证失败）
        /// </summary>
        public static HttpResponse Forbidden(string message = "Client certificate required")
        {
            return new HttpResponse
            {
                StatusCode = 403,
                Body = JsonSerializer.Serialize(Result<object>.Failed(CommonCode.Unauthorized, message))
            };
        }

        public static HttpResponse MethodNotAllowed(string message = "Method Not Allowed")
        {
            return new HttpResponse
            {
                StatusCode = 405,
                Body = JsonSerializer.Serialize(Result<object>.Failed(message))
            };
        }

        public static HttpResponse TooManyRequests(string message = "Too Many Requests")
        {
            return new HttpResponse
            {
                StatusCode = 429,
                Body = JsonSerializer.Serialize(Result<object>.Failed(message))
            };
        }

        public static HttpResponse PayloadTooLarge(string message = "Request body is too large")
        {
            return new HttpResponse
            {
                StatusCode = 413,
                Body = JsonSerializer.Serialize(Result<object>.Failed(message))
            };
        }
    }
}

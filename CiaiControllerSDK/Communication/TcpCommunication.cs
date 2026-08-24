using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Interfaces;
using CiaiControllerSDK.Logging;

namespace CiaiControllerSDK.Communication
{
    /// <summary>
    /// TCP通信实现。所有读写通过同一事务信号量，SendAndReceive不会与其他请求交叉。
    /// </summary>
    public class TcpCommunication : IFramedCommunication, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _connectTimeout;
        private readonly int _readTimeout;
        private readonly int _writeTimeout;
        private readonly SemaphoreSlim _accessSemaphore = new(1, 1);
        private readonly Queue<byte> _receiveBuffer = new();
        private readonly ILogger _logger;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _disposed;

        public bool IsConnected => !_disposed && _tcpClient?.Connected == true && _stream != null;
        public string Host => _host;
        public int Port => _port;

        public TcpCommunication(string host, int port, int timeout = 5000)
            : this(host, port, timeout, timeout, timeout)
        {
        }

        public TcpCommunication(string host, int port, int connectTimeout, int readTimeout, int writeTimeout)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("TCP主机地址不能为空", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "TCP端口必须在1到65535之间");
            if (connectTimeout <= 0 || readTimeout <= 0 || writeTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(connectTimeout), "TCP超时必须大于0");

            _host = host;
            _port = port;
            _connectTimeout = connectTimeout;
            _readTimeout = readTimeout;
            _writeTimeout = writeTimeout;
            _logger = LoggerProvider.CreateLogger<TcpCommunication>();
        }

        public async Task<bool> ConnectAsync()
        {
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(CancellationToken.None))
                return false;

            try
            {
                await DisconnectCoreAsync();
                _logger.LogDebug("正在连接TCP服务器: {Host}:{Port}, 超时: {Timeout}ms",
                    _host, _port, _connectTimeout);

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_host, _port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(_connectTimeout));
                if (completed != connectTask)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                    _logger.LogWarning("连接TCP服务器超时: {Host}:{Port}", _host, _port);
                    return false;
                }

                await connectTask;
                _stream = _tcpClient.GetStream();
                _stream.ReadTimeout = _readTimeout;
                _stream.WriteTimeout = _writeTimeout;
                _receiveBuffer.Clear();
                _logger.LogInformation("TCP连接成功: {Host}:{Port}", _host, _port);
                return true;
            }
            catch (Exception ex)
            {
                await DisconnectCoreAsync();
                _logger.LogError(ex, "连接TCP服务器失败: {Host}:{Port}", _host, _port);
                return false;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_disposed && _tcpClient == null && _stream == null)
                return;

            await _accessSemaphore.WaitAsync();
            try
            {
                await DisconnectCoreAsync();
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public Task<bool> SendAsync(byte[] data) => SendAsync(data, CancellationToken.None);

        public async Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return false;
            try
            {
                return await SendCoreAsync(data, cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public Task<byte[]> ReceiveAsync() => ReceiveAsync(CancellationToken.None);

        public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await ReceiveCoreAsync(cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public Task<byte[]> SendAndReceiveAsync(byte[] data) =>
            SendAndReceiveAsync(data, CancellationToken.None);

        public async Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await SendCoreAsync(data, cancellationToken)
                    ? await ReceiveCoreAsync(cancellationToken)
                    : null;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 读取指定长度；多读到的字节保留给下一帧。
        /// </summary>
        public async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken = default)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await ReadExactCoreAsync(length, cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 读取到结束字节（包含结束字节）；同一网络包中的后续帧会保留。
        /// </summary>
        public async Task<byte[]> ReadUntilAsync(byte endByte, int maxLength = 1024 * 1024,
            CancellationToken cancellationToken = default)
        {
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await ReadUntilCoreAsync(endByte, maxLength, cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 原子发送并读取指定长度响应。
        /// </summary>
        public async Task<byte[]> SendAndReadExactAsync(byte[] data, int length,
            CancellationToken cancellationToken = default)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await SendCoreAsync(data, cancellationToken)
                    ? await ReadExactCoreAsync(length, cancellationToken)
                    : null;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 原子发送并读取到结束字节。
        /// </summary>
        public async Task<byte[]> SendAndReadUntilAsync(byte[] data, byte endByte,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default)
        {
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await SendCoreAsync(data, cancellationToken)
                    ? await ReadUntilCoreAsync(endByte, maxLength, cancellationToken)
                    : null;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public async Task<byte[]> ReadUntilAsync(byte[] delimiter, int maxLength = 1024 * 1024,
            CancellationToken cancellationToken = default)
        {
            ValidateDelimiter(delimiter, maxLength);
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken)) return null;
            try { return await ReadUntilCoreAsync(delimiter, maxLength, cancellationToken); }
            finally { _accessSemaphore.Release(); }
        }

        public async Task<byte[]> SendAndReadUntilAsync(byte[] data, byte[] delimiter,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default)
        {
            ValidateDelimiter(delimiter, maxLength);
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken)) return null;
            try { return await SendCoreAsync(data, cancellationToken)
                    ? await ReadUntilCoreAsync(delimiter, maxLength, cancellationToken) : null; }
            finally { _accessSemaphore.Release(); }
        }

        private async Task<bool> SendCoreAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!IsConnected)
            {
                _logger.LogWarning("发送失败: TCP未连接");
                return false;
            }

            try
            {
                using var timeout = CreateTimeout(cancellationToken, _writeTimeout);
                await _stream.WriteAsync(data.AsMemory(0, data.Length), timeout.Token);
                await _stream.FlushAsync(timeout.Token);
                _logger.LogDebug("TCP发送数据: {Length}字节", data.Length);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException)
            {
                _logger.LogError(ex, "TCP发送数据失败: {Length}字节", data.Length);
                return false;
            }
        }

        private async Task<byte[]> ReceiveCoreAsync(CancellationToken cancellationToken)
        {
            if (_receiveBuffer.Count > 0)
                return Dequeue(_receiveBuffer.Count);
            return await ReadNetworkChunkAsync(cancellationToken);
        }

        private async Task<byte[]> ReadExactCoreAsync(int length, CancellationToken cancellationToken)
        {
            while (_receiveBuffer.Count < length)
            {
                var chunk = await ReadNetworkChunkAsync(cancellationToken);
                if (chunk == null)
                    return null;
                foreach (var value in chunk)
                    _receiveBuffer.Enqueue(value);
            }

            return Dequeue(length);
        }

        private async Task<byte[]> ReadUntilCoreAsync(byte endByte, int maxLength,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var buffered = _receiveBuffer.ToArray();
                var endIndex = Array.IndexOf(buffered, endByte);
                if (endIndex >= 0)
                    return Dequeue(endIndex + 1);
                if (_receiveBuffer.Count >= maxLength)
                    throw new InvalidOperationException($"TCP帧超过最大长度: {maxLength}");

                var chunk = await ReadNetworkChunkAsync(cancellationToken);
                if (chunk == null)
                    return null;
                foreach (var value in chunk)
                    _receiveBuffer.Enqueue(value);
            }
        }

        private async Task<byte[]> ReadUntilCoreAsync(byte[] delimiter, int maxLength,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var buffered = _receiveBuffer.ToArray();
                var index = IndexOf(buffered, delimiter);
                if (index >= 0) return Dequeue(index + delimiter.Length);
                if (_receiveBuffer.Count >= maxLength)
                    throw new InvalidOperationException($"TCP帧超过最大长度: {maxLength}");
                var chunk = await ReadNetworkChunkAsync(cancellationToken);
                if (chunk == null) return null;
                foreach (var value in chunk) _receiveBuffer.Enqueue(value);
            }
        }

        private static void ValidateDelimiter(byte[] delimiter, int maxLength)
        {
            if (delimiter == null || delimiter.Length == 0) throw new ArgumentException("帧分隔符不能为空", nameof(delimiter));
            if (maxLength < delimiter.Length) throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        private static int IndexOf(byte[] source, byte[] pattern)
        {
            for (var i = 0; i <= source.Length - pattern.Length; i++)
            {
                var match = true;
                for (var j = 0; j < pattern.Length; j++) if (source[i + j] != pattern[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        private async Task<byte[]> ReadNetworkChunkAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("接收失败: TCP未连接");
                return null;
            }

            try
            {
                var buffer = new byte[4096];
                using var timeout = CreateTimeout(cancellationToken, _readTimeout);
                var bytesRead = await _stream.ReadAsync(buffer.AsMemory(), timeout.Token);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("TCP连接已关闭（收到0字节）");
                    return null;
                }

                var result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                _logger.LogDebug("TCP接收数据: {Length}字节", bytesRead);
                return result;
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException)
            {
                _logger.LogError(ex, "TCP接收数据失败");
                return null;
            }
        }

        private Task DisconnectCoreAsync()
        {
            if (_stream != null || _tcpClient != null)
                _logger.LogInformation("断开TCP连接: {Host}:{Port}", _host, _port);
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _stream = null;
            _tcpClient = null;
            _receiveBuffer.Clear();
            return Task.CompletedTask;
        }

        private byte[] Dequeue(int count)
        {
            var result = new byte[count];
            for (var index = 0; index < count; index++)
                result[index] = _receiveBuffer.Dequeue();
            return result;
        }

        private async Task<bool> WaitForAccessAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _accessSemaphore.WaitAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken, int timeoutMs)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            source.CancelAfter(timeoutMs);
            return source;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpCommunication));
        }

        public bool Connect() => ConnectAsync().GetAwaiter().GetResult();
        public void Disconnect() => DisconnectAsync().GetAwaiter().GetResult();
        public bool Send(byte[] data) => SendAsync(data).GetAwaiter().GetResult();
        public byte[] Receive() => ReceiveAsync().GetAwaiter().GetResult();
        public byte[] SendAndReceive(byte[] data) => SendAndReceiveAsync(data).GetAwaiter().GetResult();
        public byte[] ReadExact(int length) => ReadExactAsync(length).GetAwaiter().GetResult();
        public byte[] ReadUntil(byte endByte, int maxLength = 1024 * 1024) =>
            ReadUntilAsync(endByte, maxLength).GetAwaiter().GetResult();
        public byte[] SendAndReadExact(byte[] data, int length) =>
            SendAndReadExactAsync(data, length).GetAwaiter().GetResult();
        public byte[] SendAndReadUntil(byte[] data, byte endByte, int maxLength = 1024 * 1024) =>
            SendAndReadUntilAsync(data, endByte, maxLength).GetAwaiter().GetResult();

        public void Dispose()
        {
            if (_disposed)
                return;
            Disconnect();
            _disposed = true;
            _accessSemaphore.Dispose();
        }
    }
}

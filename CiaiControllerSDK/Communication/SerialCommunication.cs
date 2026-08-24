using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CiaiControllerSDK.Interfaces;
using CiaiControllerSDK.Logging;

namespace CiaiControllerSDK.Communication
{
    /// <summary>
    /// 串口通信实现。所有读写通过同一事务信号量，避免多个接口交叉收发。
    /// </summary>
    public class SerialCommunication : IFramedCommunication, IDisposable
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;
        private readonly int _readTimeout;
        private readonly int _writeTimeout;
        private readonly Encoding _encoding;
        private readonly Handshake _handshake;
        private readonly bool _dtrEnable;
        private readonly bool _rtsEnable;
        private readonly bool _discardInputBeforeWrite;
        private readonly SemaphoreSlim _accessSemaphore = new(1, 1);
        private readonly ILogger _logger;
        private SerialPort _serialPort;
        private bool _disposed;

        public bool IsConnected => !_disposed && _serialPort?.IsOpen == true;
        public string PortName => _portName;
        public int BaudRate => _baudRate;

        public SerialCommunication(string portName, int baudRate = 9600,
            Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One,
            int timeout = 5000, Encoding encoding = null)
            : this(portName, baudRate, parity, dataBits, stopBits, timeout, timeout, encoding)
        {
        }

        public SerialCommunication(string portName, int baudRate, Parity parity, int dataBits,
            StopBits stopBits, int readTimeout, int writeTimeout, Encoding encoding = null,
            Handshake handshake = Handshake.None, bool dtrEnable = false, bool rtsEnable = false,
            bool discardInputBeforeWrite = false)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("串口名称不能为空", nameof(portName));
            if (baudRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(baudRate));
            if (dataBits < 5 || dataBits > 8)
                throw new ArgumentOutOfRangeException(nameof(dataBits), "串口数据位必须在5到8之间");
            if (stopBits == StopBits.None)
                throw new ArgumentOutOfRangeException(nameof(stopBits), "串口停止位不能为None");
            if (readTimeout <= 0 || writeTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(readTimeout), "串口超时必须大于0");

            _portName = portName;
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _readTimeout = readTimeout;
            _writeTimeout = writeTimeout;
            _encoding = encoding ?? Encoding.UTF8;
            _handshake = handshake;
            _dtrEnable = dtrEnable;
            _rtsEnable = rtsEnable;
            _discardInputBeforeWrite = discardInputBeforeWrite;
            _logger = LoggerProvider.CreateLogger<SerialCommunication>();
        }

        public async Task<bool> ConnectAsync()
        {
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(CancellationToken.None))
                return false;
            try
            {
                DisconnectCore();
                _logger.LogDebug(
                    "正在打开串口: {PortName}, {BaudRate}/{DataBits}/{Parity}/{StopBits}",
                    _portName, _baudRate, _dataBits, _parity, _stopBits);

                _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
                {
                    ReadTimeout = _readTimeout,
                    WriteTimeout = _writeTimeout,
                    Encoding = _encoding,
                    Handshake = _handshake,
                    DtrEnable = _dtrEnable,
                    RtsEnable = _rtsEnable
                };
                _serialPort.Open();
                _logger.LogInformation("串口打开成功: {PortName}", _portName);
                return true;
            }
            catch (Exception ex)
            {
                DisconnectCore();
                _logger.LogError(ex, "打开串口失败: {PortName}", _portName);
                return false;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_disposed && _serialPort == null)
                return;
            await _accessSemaphore.WaitAsync();
            try
            {
                DisconnectCore();
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

        public Task<bool> SendCommandAsync(string command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            return SendAsync(_encoding.GetBytes(command));
        }

        public async Task<string> ReceiveResponseAsync()
        {
            var data = await ReceiveAsync();
            return data == null ? null : _encoding.GetString(data);
        }

        public async Task<string> SendAndReceiveStringAsync(string command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            var data = await SendAndReceiveAsync(_encoding.GetBytes(command));
            return data == null ? null : _encoding.GetString(data);
        }

        /// <summary>
        /// 循环读取，直到遇到帧尾字节或超时。整个帧读取期间保持独占访问。
        /// </summary>
        public async Task<byte[]> ReadResponseAsync(int frameEndByte, int timeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await ReadUntilCoreAsync((byte)(frameEndByte & 0xFF), timeoutMs, 1024 * 1024,
                    cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 读取指定长度的串口帧。
        /// </summary>
        public async Task<byte[]> ReadExactAsync(int length, int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            var timeout = timeoutMs ?? _readTimeout;
            if (timeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await ReadExactCoreAsync(length, timeout, cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        async Task<byte[]> IFramedCommunication.ReadExactAsync(int length,
            CancellationToken cancellationToken) =>
            await ReadExactAsync(length, null, cancellationToken);

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
                return await ReadUntilCoreAsync(endByte, _readTimeout, maxLength, cancellationToken);
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        /// <summary>
        /// 原子发送并按结束字节读取响应。
        /// </summary>
        public async Task<byte[]> SendAndReadUntilAsync(byte[] data, int frameEndByte,
            int? timeoutMs = null, CancellationToken cancellationToken = default)
        {
            var timeout = timeoutMs ?? _readTimeout;
            if (timeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await SendCoreAsync(data, cancellationToken)
                    ? await ReadUntilCoreAsync((byte)(frameEndByte & 0xFF), timeout, 1024 * 1024,
                        cancellationToken)
                    : null;
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
            int? timeoutMs = null, CancellationToken cancellationToken = default)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            var timeout = timeoutMs ?? _readTimeout;
            if (timeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken))
                return null;
            try
            {
                return await SendCoreAsync(data, cancellationToken)
                    ? await ReadExactCoreAsync(length, timeout, cancellationToken)
                    : null;
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        async Task<byte[]> IFramedCommunication.SendAndReadExactAsync(byte[] data, int length,
            CancellationToken cancellationToken) =>
            await SendAndReadExactAsync(data, length, null, cancellationToken);

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
                    ? await ReadUntilCoreAsync(endByte, _readTimeout, maxLength, cancellationToken)
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
            try { return await ReadUntilCoreAsync(delimiter, _readTimeout, maxLength, cancellationToken); }
            finally { _accessSemaphore.Release(); }
        }

        public async Task<byte[]> SendAndReadUntilAsync(byte[] data, byte[] delimiter,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default)
        {
            ValidateDelimiter(delimiter, maxLength);
            ThrowIfDisposed();
            if (!await WaitForAccessAsync(cancellationToken)) return null;
            try { return await SendCoreAsync(data, cancellationToken)
                    ? await ReadUntilCoreAsync(delimiter, _readTimeout, maxLength, cancellationToken) : null; }
            finally { _accessSemaphore.Release(); }
        }

        public void ClearBuffer()
        {
            ThrowIfDisposed();
            _accessSemaphore.Wait();
            try
            {
                if (IsConnected)
                    _serialPort.DiscardInBuffer();
            }
            finally
            {
                _accessSemaphore.Release();
            }
        }

        private async Task<bool> SendCoreAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!IsConnected)
            {
                _logger.LogWarning("发送失败: 串口未打开");
                return false;
            }

            try
            {
                if (_discardInputBeforeWrite)
                    _serialPort.DiscardInBuffer();
                await Task.Run(() => _serialPort.Write(data, 0, data.Length), cancellationToken);
                _logger.LogDebug("串口发送数据: {Length}字节", data.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "串口发送数据失败: {Length}字节", data.Length);
                return false;
            }
        }

        private async Task<byte[]> ReceiveCoreAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("接收失败: 串口未打开");
                return null;
            }

            try
            {
                return await Task.Run(() =>
                {
                    var buffer = new byte[4096];
                    var bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                        return null;
                    var result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "串口接收数据失败");
                return null;
            }
        }

        private async Task<byte[]> ReadUntilCoreAsync(byte endByte, int timeoutMs, int maxLength,
            CancellationToken cancellationToken)
        {
            if (!IsConnected)
                return null;
            var result = new List<byte>();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var value = ReadAvailableByte();
                if (value >= 0)
                {
                    result.Add((byte)value);
                    if ((byte)value == endByte)
                        return result.ToArray();
                    if (result.Count >= maxLength)
                        throw new InvalidOperationException($"串口帧超过最大长度: {maxLength}");
                }
                else if (!await DelayForDataAsync(cancellationToken))
                {
                    return null;
                }
            }

            _logger.LogWarning("串口读取帧超时: {Timeout}ms, 已收到 {Length} 字节", timeoutMs, result.Count);
            return null;
        }

        private async Task<byte[]> ReadExactCoreAsync(int length, int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (!IsConnected)
                return null;
            var result = new List<byte>(length);
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (result.Count < length && DateTime.UtcNow < deadline)
            {
                var value = ReadAvailableByte();
                if (value >= 0)
                    result.Add((byte)value);
                else if (!await DelayForDataAsync(cancellationToken))
                    return null;
            }

            return result.Count == length ? result.ToArray() : null;
        }

        private async Task<byte[]> ReadUntilCoreAsync(byte[] delimiter, int timeoutMs, int maxLength,
            CancellationToken cancellationToken)
        {
            var result = new List<byte>();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && result.Count < maxLength)
            {
                var value = ReadAvailableByte();
                if (value >= 0)
                {
                    result.Add((byte)value);
                    if (result.Count >= delimiter.Length)
                    {
                        var match = true;
                        for (var i = 0; i < delimiter.Length; i++)
                            if (result[result.Count - delimiter.Length + i] != delimiter[i]) { match = false; break; }
                        if (match) return result.ToArray();
                    }
                }
                else if (!await DelayForDataAsync(cancellationToken)) return null;
            }
            return null;
        }

        private static void ValidateDelimiter(byte[] delimiter, int maxLength)
        {
            if (delimiter == null || delimiter.Length == 0) throw new ArgumentException("帧分隔符不能为空", nameof(delimiter));
            if (maxLength < delimiter.Length) throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        private int ReadAvailableByte()
        {
            return _serialPort?.IsOpen == true && _serialPort.BytesToRead > 0
                ? _serialPort.ReadByte()
                : -1;
        }

        private static async Task<bool> DelayForDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(20, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private void DisconnectCore()
        {
            if (_serialPort != null)
                _logger.LogInformation("关闭串口: {PortName}", _portName);
            try
            {
                _serialPort?.Close();
            }
            finally
            {
                _serialPort?.Dispose();
                _serialPort = null;
            }
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SerialCommunication));
        }

        public bool Connect() => ConnectAsync().GetAwaiter().GetResult();
        public void Disconnect() => DisconnectAsync().GetAwaiter().GetResult();
        public bool Send(byte[] data) => SendAsync(data).GetAwaiter().GetResult();
        public byte[] Receive() => ReceiveAsync().GetAwaiter().GetResult();
        public byte[] SendAndReceive(byte[] data) => SendAndReceiveAsync(data).GetAwaiter().GetResult();
        public bool SendCommand(string command) => SendCommandAsync(command).GetAwaiter().GetResult();
        public string ReceiveResponse() => ReceiveResponseAsync().GetAwaiter().GetResult();
        public string SendAndReceiveString(string command) =>
            SendAndReceiveStringAsync(command).GetAwaiter().GetResult();
        public byte[] ReadResponse(int frameEndByte, int timeoutMs) =>
            ReadResponseAsync(frameEndByte, timeoutMs).GetAwaiter().GetResult();
        public byte[] ReadExact(int length, int? timeoutMs = null) =>
            ReadExactAsync(length, timeoutMs).GetAwaiter().GetResult();
        public byte[] SendAndReadUntil(byte[] data, int frameEndByte, int? timeoutMs = null) =>
            SendAndReadUntilAsync(data, frameEndByte, timeoutMs).GetAwaiter().GetResult();
        public byte[] SendAndReadExact(byte[] data, int length, int? timeoutMs = null) =>
            SendAndReadExactAsync(data, length, timeoutMs).GetAwaiter().GetResult();

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

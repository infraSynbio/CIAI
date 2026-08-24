using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Interfaces;

namespace CiaiControllerSDK.Communication
{
    /// <summary>
    /// Isolates legacy DLL/COM SDKs in a child process. The wire format is a
    /// little-endian Int32 length followed by the raw payload. Stdout is reserved
    /// for protocol frames; adapter diagnostics must be written to stderr.
    /// </summary>
    public sealed class ProcessCommunication : ICommunication, IDisposable
    {
        private const int MaxFrameLength = 64 * 1024 * 1024;
        private readonly ConnectionConfiguration _configuration;
        private readonly SemaphoreSlim _transaction = new(1, 1);
        private readonly ConcurrentQueue<string> _stderrTail = new();
        private Process _process;
        private Stream _input;
        private Stream _output;
        private bool _disposed;

        public ProcessCommunication(ConnectionConfiguration configuration) =>
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public bool IsConnected => !_disposed && _process != null && !_process.HasExited &&
                                   _input != null && _output != null;

        public async Task<bool> ConnectAsync()
        {
            ThrowIfDisposed();
            await _transaction.WaitAsync();
            try
            {
                if (IsConnected) return true;
                DisconnectCore();
                while (_stderrTail.TryDequeue(out _)) { }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _configuration.Executable,
                    WorkingDirectory = string.IsNullOrWhiteSpace(_configuration.WorkingDirectory)
                        ? Path.GetDirectoryName(Path.GetFullPath(_configuration.Executable)) ?? Environment.CurrentDirectory
                        : _configuration.WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                foreach (var argument in _configuration.Arguments)
                    startInfo.ArgumentList.Add(argument);
                foreach (var pair in _configuration.Environment)
                    startInfo.Environment[pair.Key] = pair.Value;

                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                if (!_process.Start()) return false;
                _input = _process.StandardOutput.BaseStream;
                _output = _process.StandardInput.BaseStream;
                _ = DrainStandardErrorAsync(_process, _stderrTail);
                return true;
            }
            catch
            {
                DisconnectCore();
                return false;
            }
            finally { _transaction.Release(); }
        }

        public async Task DisconnectAsync()
        {
            await _transaction.WaitAsync();
            try { DisconnectCore(); }
            finally { _transaction.Release(); }
        }

        public async Task<bool> SendAsync(byte[] data)
        {
            await _transaction.WaitAsync();
            try
            {
                using var timeout = CreateTimeout(_configuration.WriteTimeoutMs);
                await WriteFrameAsync(data, timeout.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                DisconnectCore();
                throw new TimeoutException(
                    $"Process adapter write timed out after {_configuration.WriteTimeoutMs} ms.{FormatStderr()}");
            }
            finally { _transaction.Release(); }
        }

        public async Task<byte[]> ReceiveAsync()
        {
            await _transaction.WaitAsync();
            try
            {
                using var timeout = CreateTimeout(_configuration.ReadTimeoutMs);
                return await ReadFrameAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                DisconnectCore();
                throw new TimeoutException(
                    $"Process adapter read timed out after {_configuration.ReadTimeoutMs} ms.{FormatStderr()}");
            }
            finally { _transaction.Release(); }
        }

        public async Task<byte[]> SendAndReceiveAsync(byte[] data)
        {
            await _transaction.WaitAsync();
            try
            {
                using (var writeTimeout = CreateTimeout(_configuration.WriteTimeoutMs))
                    await WriteFrameAsync(data, writeTimeout.Token);
                using var readTimeout = CreateTimeout(_configuration.ReadTimeoutMs);
                return await ReadFrameAsync(readTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                DisconnectCore();
                throw new TimeoutException(
                    $"Process adapter request timed out (write {_configuration.WriteTimeoutMs} ms, read {_configuration.ReadTimeoutMs} ms).{FormatStderr()}");
            }
            catch
            {
                // A partial request/response frame cannot be reused safely.
                DisconnectCore();
                throw;
            }
            finally { _transaction.Release(); }
        }

        private async Task WriteFrameAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (!IsConnected) throw new IOException("Vendor adapter process is not connected.");
            data ??= Array.Empty<byte>();
            if (data.Length > MaxFrameLength) throw new IOException("Request frame is too large.");
            var prefix = BitConverter.GetBytes(data.Length);
            await _output.WriteAsync(prefix, 0, prefix.Length, cancellationToken);
            await _output.WriteAsync(data, 0, data.Length, cancellationToken);
            await _output.FlushAsync(cancellationToken);
        }

        private async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected) throw new IOException("Vendor adapter process is not connected.");
            var prefix = await ReadExactAsync(4, cancellationToken);
            var length = BitConverter.ToInt32(prefix, 0);
            if (length < 0 || length > MaxFrameLength)
                throw new IOException($"Invalid response frame length: {length}.{FormatStderr()}");
            return await ReadExactAsync(length, cancellationToken);
        }

        private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
        {
            var result = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await _input.ReadAsync(result, offset, length - offset, cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException("Vendor adapter process closed its output." + FormatStderr());
                offset += read;
            }
            return result;
        }

        private static async Task DrainStandardErrorAsync(Process process, ConcurrentQueue<string> tail)
        {
            try
            {
                while (!process.HasExited)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line == null) return;
                    tail.Enqueue(line);
                    while (tail.Count > 50) tail.TryDequeue(out _);
                }
            }
            catch { }
        }

        private static CancellationTokenSource CreateTimeout(int timeoutMs)
        {
            var source = new CancellationTokenSource();
            source.CancelAfter(Math.Max(1, timeoutMs));
            return source;
        }

        private string FormatStderr()
        {
            var lines = _stderrTail.ToArray();
            return lines.Length == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, lines);
        }

        private void DisconnectCore()
        {
            try { _output?.Dispose(); } catch { }
            try { _input?.Dispose(); } catch { }
            _output = null;
            _input = null;
            if (_process == null) return;
            try
            {
                if (!_process.HasExited && !_process.WaitForExit(_configuration.ShutdownTimeoutMs))
                    _process.Kill(entireProcessTree: true);
            }
            catch { }
            _process.Dispose();
            _process = null;
        }

        public bool Connect() => ConnectAsync().GetAwaiter().GetResult();
        public void Disconnect() => DisconnectAsync().GetAwaiter().GetResult();
        public bool Send(byte[] data) => SendAsync(data).GetAwaiter().GetResult();
        public byte[] Receive() => ReceiveAsync().GetAwaiter().GetResult();
        public byte[] SendAndReceive(byte[] data) => SendAndReceiveAsync(data).GetAwaiter().GetResult();

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessCommunication));
        }

        public void Dispose()
        {
            if (_disposed) return;
            DisconnectCore();
            _transaction.Dispose();
            _disposed = true;
        }
    }
}

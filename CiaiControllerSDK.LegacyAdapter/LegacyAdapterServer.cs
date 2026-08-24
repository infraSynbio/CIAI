using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CiaiControllerSDK.LegacyAdapter
{
    /// <summary>
    /// 厂商DLL适配器进程的标准输入输出循环。stdout只用于协议，日志必须写stderr。
    /// 可由net472/x86/STA可执行程序引用本库。
    /// </summary>
    public static class LegacyAdapterServer
    {
        private const int MaxFrameLength = 64 * 1024 * 1024;

        public static void Run(Func<byte[], byte[]> handler) =>
            RunAsync(request => Task.FromResult(handler(request))).GetAwaiter().GetResult();

        public static async Task RunAsync(Func<byte[], Task<byte[]>> handler,
            CancellationToken cancellationToken = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            using (var input = Console.OpenStandardInput())
            using (var output = Console.OpenStandardOutput())
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var prefix = await ReadExactOrEofAsync(input, 4, cancellationToken).ConfigureAwait(false);
                    if (prefix == null) return;
                    var length = BitConverter.ToInt32(prefix, 0);
                    if (length < 0 || length > MaxFrameLength)
                        throw new InvalidDataException("Invalid request length: " + length);
                    var request = await ReadExactOrEofAsync(input, length, cancellationToken).ConfigureAwait(false)
                                  ?? throw new EndOfStreamException();
                    byte[] response;
                    try { response = await handler(request).ConfigureAwait(false) ?? Array.Empty<byte>(); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                        response = Array.Empty<byte>();
                    }
                    var responsePrefix = BitConverter.GetBytes(response.Length);
                    await output.WriteAsync(responsePrefix, 0, 4, cancellationToken).ConfigureAwait(false);
                    await output.WriteAsync(response, 0, response.Length, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<byte[]> ReadExactOrEofAsync(Stream input, int length,
            CancellationToken cancellationToken)
        {
            var result = new byte[length]; var offset = 0;
            while (offset < length)
            {
                var read = await input.ReadAsync(result, offset, length - offset, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) return offset == 0 ? null : throw new EndOfStreamException();
                offset += read;
            }
            return result;
        }
    }
}

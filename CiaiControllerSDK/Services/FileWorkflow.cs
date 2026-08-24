using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CiaiControllerSDK.Services
{
    /// <summary>为文件导入/导出型设备提供路径隔离、文件稳定检测和原子写入。</summary>
    public sealed class FileWorkflow
    {
        public string RootDirectory { get; }
        public FileWorkflow(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("文件工作目录不能为空", nameof(rootDirectory));
            RootDirectory = Path.GetFullPath(rootDirectory);
            Directory.CreateDirectory(RootDirectory);
        }

        public string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("相对路径不能为空", nameof(relativePath));
            var full = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
            var prefix = RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("文件路径超出允许的工作目录");
            return full;
        }

        public async Task<string> WaitForStableFileAsync(string relativePath, TimeSpan timeout,
            TimeSpan? stablePeriod = null, CancellationToken cancellationToken = default)
        {
            var path = Resolve(relativePath); var stable = stablePeriod ?? TimeSpan.FromMilliseconds(500);
            var deadline = DateTime.UtcNow + timeout; long previous = -1; DateTime unchangedSince = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    var length = new FileInfo(path).Length;
                    if (length == previous && DateTime.UtcNow - unchangedSince >= stable) return path;
                    if (length != previous) { previous = length; unchangedSince = DateTime.UtcNow; }
                }
                await Task.Delay(100, cancellationToken);
            }
            throw new TimeoutException($"等待文件稳定超时: {relativePath}");
        }

        public async Task WriteAtomicAsync(string relativePath, byte[] data,
            CancellationToken cancellationToken = default)
        {
            var target = Resolve(relativePath); Directory.CreateDirectory(Path.GetDirectoryName(target));
            var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 81920, true))
                    await stream.WriteAsync(data ?? Array.Empty<byte>(), cancellationToken);
                File.Move(temporary, target, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

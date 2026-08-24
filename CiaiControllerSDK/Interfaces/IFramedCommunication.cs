using System.Threading;
using System.Threading.Tasks;

namespace CiaiControllerSDK.Interfaces
{
    /// <summary>
    /// 支持定长帧和结束字节帧的单通道通信；发送及其响应必须由同一个传输事务锁保护。
    /// </summary>
    public interface IFramedCommunication : ICommunication
    {
        Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken = default);
        Task<byte[]> ReadUntilAsync(byte endByte, int maxLength = 1024 * 1024,
            CancellationToken cancellationToken = default);
        Task<byte[]> SendAndReadExactAsync(byte[] data, int length,
            CancellationToken cancellationToken = default);
        Task<byte[]> SendAndReadUntilAsync(byte[] data, byte endByte,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default);
        Task<byte[]> ReadUntilAsync(byte[] delimiter, int maxLength = 1024 * 1024,
            CancellationToken cancellationToken = default);
        Task<byte[]> SendAndReadUntilAsync(byte[] data, byte[] delimiter,
            int maxLength = 1024 * 1024, CancellationToken cancellationToken = default);
    }
}

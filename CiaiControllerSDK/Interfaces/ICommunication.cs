using System.Threading.Tasks;

namespace CiaiControllerSDK.Interfaces
{
    /// <summary>
    /// 通信接口基类
    /// </summary>
    public interface ICommunication
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        #region 异步方法

        /// <summary>
        /// 连接设备
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 发送数据
        /// </summary>
        Task<bool> SendAsync(byte[] data);

        /// <summary>
        /// 接收数据
        /// </summary>
        Task<byte[]> ReceiveAsync();

        /// <summary>
        /// 发送并接收
        /// </summary>
        Task<byte[]> SendAndReceiveAsync(byte[] data);

        #endregion

        #region 同步方法

        /// <summary>
        /// 连接设备（同步）
        /// </summary>
        bool Connect();

        /// <summary>
        /// 断开连接（同步）
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 发送数据（同步）
        /// </summary>
        bool Send(byte[] data);

        /// <summary>
        /// 接收数据（同步）
        /// </summary>
        byte[] Receive();

        /// <summary>
        /// 发送并接收（同步）
        /// </summary>
        byte[] SendAndReceive(byte[] data);

        #endregion
    }
}

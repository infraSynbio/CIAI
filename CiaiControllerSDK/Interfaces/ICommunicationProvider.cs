using System.Collections.Generic;
using CiaiControllerSDK.Core;

namespace CiaiControllerSDK.Interfaces
{
    /// <summary>厂商协议或第三方通信实现的注册入口，无需修改SDK工厂。</summary>
    public interface ICommunicationProvider
    {
        IEnumerable<string> Types { get; }
        void Validate(ConnectionConfiguration configuration);
        ICommunication Create(ConnectionConfiguration configuration);
    }
}

using System.Text.Json.Serialization;

namespace CiaiControllerSDK.Models
{
    /// <summary>
    /// 操作接口数据
    /// </summary>
    public class OperationData
    {
        /// <summary>
        /// 操作名称
        /// </summary>
        [JsonPropertyName("operationName")]
        public string OperationName { get; set; }

        /// <summary>
        /// 操作参数
        /// </summary>
        [JsonPropertyName("operationParam")]
        public object OperationParam { get; set; }
    }

    /// <summary>
    /// 设置接口数据
    /// </summary>
    public class SetData
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        [JsonPropertyName("setName")]
        public string SetName { get; set; }

        /// <summary>
        /// 参数值
        /// </summary>
        [JsonPropertyName("setValue")]
        public string SetValue { get; set; }
    }

    /// <summary>
    /// 获取状态返回数据
    /// </summary>
    public class GetReturn
    {
        /// <summary>
        /// 状态名称
        /// </summary>
        [JsonPropertyName("getName")]
        public string GetName { get; set; }

        /// <summary>
        /// 状态值
        /// </summary>
        [JsonPropertyName("getValue")]
        public string GetValue { get; set; }
    }

    /// <summary>
    /// 进出接口数据
    /// </summary>
    public class EnterOrExitData
    {
        /// <summary>
        /// 进出操作名称
        /// </summary>
        [JsonPropertyName("enterOrExitName")]
        public string EnterOrExitName { get; set; }

        /// <summary>
        /// 进出操作值
        /// </summary>
        [JsonPropertyName("enterOrExitValue")]
        public object EnterOrExitValue { get; set; }
    }
}

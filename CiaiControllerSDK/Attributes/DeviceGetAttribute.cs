using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个方法为状态获取
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class DeviceGetAttribute : Attribute
    {
        /// <summary>
        /// 状态名称（对应Get接口的getName）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 状态标题（中文）
        /// </summary>
        public string TitleCN { get; set; }

        /// <summary>
        /// 状态标题（英文）
        /// </summary>
        public string TitleEN { get; set; }

        /// <summary>
        /// 状态类型: boolean, string, int, float
        /// </summary>
        public string Type { get; set; } = "string";

        /// <summary>
        /// 状态单位
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 状态描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public DeviceGetAttribute(string name)
        {
            Name = name;
        }
    }
}

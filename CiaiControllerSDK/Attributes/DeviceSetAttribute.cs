using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个方法为参数设置
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class DeviceSetAttribute : Attribute
    {
        /// <summary>
        /// 参数名称（对应Set接口的setName）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数标题（中文）
        /// </summary>
        public string TitleCN { get; set; }

        /// <summary>
        /// 参数标题（英文）
        /// </summary>
        public string TitleEN { get; set; }

        /// <summary>
        /// 参数类型: input, select
        /// </summary>
        public string Type { get; set; } = "input";

        /// <summary>
        /// 参数单位
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public DeviceSetAttribute(string name)
        {
            Name = name;
        }
    }
}

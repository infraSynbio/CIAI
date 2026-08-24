using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个方法为设备操作
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class DeviceOperationAttribute : Attribute
    {
        /// <summary>
        /// 操作名称（对应Operation接口的operationName）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 操作标题（中文）
        /// </summary>
        public string TitleCN { get; set; }

        /// <summary>
        /// 操作标题（英文）
        /// </summary>
        public string TitleEN { get; set; }

        /// <summary>
        /// 操作描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 动态表单JSON定义
        /// 用于前端渲染参数输入表单
        /// </summary>
        public string FormJson { get; set; } = string.Empty;

        public DeviceOperationAttribute(string name)
        {
            Name = name;
        }
    }
}

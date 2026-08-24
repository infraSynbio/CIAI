using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个方法为设备功能
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class DeviceFunctionAttribute : Attribute
    {
        /// <summary>
        /// 功能名称（对应Function接口的functionName）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 功能标题（中文）
        /// </summary>
        public string TitleCN { get; set; }

        /// <summary>
        /// 功能标题（英文）
        /// </summary>
        public string TitleEN { get; set; }

        /// <summary>
        /// 功能描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 功能分类（中文）
        /// </summary>
        public string CategoryCN { get; set; } = string.Empty;

        /// <summary>
        /// 功能分类（英文）
        /// </summary>
        public string CategoryEN { get; set; } = string.Empty;

        /// <summary>
        /// 默认执行时间（秒）
        /// </summary>
        public int DefaultPeriod { get; set; } = 60;

        /// <summary>
        /// 动态表单JSON定义
        /// 用于前端渲染参数输入表单
        /// </summary>
        public string FormJson { get; set; } = string.Empty;

        /// <summary>
        /// 功能图标（黑色版本，Base64编码）
        /// 直接设置Base64字符串
        /// </summary>
        public string IconBlack { get; set; } = string.Empty;

        /// <summary>
        /// 功能图标（白色版本，Base64编码）
        /// 直接设置Base64字符串
        /// </summary>
        public string IconWhite { get; set; } = string.Empty;

        /// <summary>
        /// 功能图标文件名 - 黑色版本（相对于icon文件夹）
        /// 例如: "icon_组件_默认图标_黑色版.png"
        /// </summary>
        public string IconFileBlack { get; set; } = string.Empty;

        /// <summary>
        /// 功能图标文件名 - 白色版本（相对于icon文件夹）
        /// 例如: "icon_组件_默认图标_白色版.png"
        /// </summary>
        public string IconFileWhite { get; set; } = string.Empty;

        public DeviceFunctionAttribute(string name)
        {
            Name = name;
        }
    }
}

using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个方法为进出操作
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class DeviceEnterExitAttribute : Attribute
    {
        /// <summary>
        /// 操作名称（对应EnterAndExit接口的enterOrExitName）
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

        public DeviceEnterExitAttribute(string name)
        {
            Name = name;
        }
    }
}

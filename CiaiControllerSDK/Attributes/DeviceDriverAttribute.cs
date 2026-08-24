using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 标记一个类为设备驱动
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DeviceDriverAttribute : Attribute
    {
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备英文名称
        /// </summary>
        public string NameEN { get; set; }

        /// <summary>
        /// 设备型号
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 设备制造商
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// 驱动版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 设备类型: 1-核心设备 2-转移设备 3-辅助设备 4-存储设备
        /// </summary>
        public int EquipmentType { get; set; } = 1;

        /// <summary>
        /// 设备功能资源数（同一时刻可执行的功能数）
        /// </summary>
        public int FunctionalResources { get; set; } = 1;

        /// <summary>
        /// 是否支持急停
        /// </summary>
        public bool CanEmergencyStop { get; set; } = true;

        /// <summary>
        /// 运行时可访问性
        /// </summary>
        public int RuntimeAccessibility { get; set; } = 1;

        /// <summary>
        /// 是否可并行执行功能
        /// </summary>
        public int Parallelizability { get; set; } = 0;

        /// <summary>
        /// 设备图标（Base64编码）
        /// 直接设置Base64字符串
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 设备图标文件名（相对于icon文件夹）
        /// 例如: "设备默认图片.png"
        /// </summary>
        public string IconFile { get; set; } = string.Empty;

        /// <summary>
        /// 驱动作者
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 设备分类
        /// </summary>
        public string EquipmentClass { get; set; } = string.Empty;

        public DeviceDriverAttribute(string name)
        {
            Name = name;
        }
    }
}

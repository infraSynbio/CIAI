using System;

namespace CiaiControllerSDK.Attributes
{
    /// <summary>
    /// 设备位置属性标注
    /// 用于标记返回 EquipmentNest 的属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class DeviceNestAttribute : Attribute
    {
        /// <summary>
        /// 位置顺序（可选，用于排序）
        /// </summary>
        public int Order { get; set; }
    }
}

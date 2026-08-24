using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CiaiControllerSDK.Models
{
    /// <summary>
    /// 心跳信息
    /// </summary>
    public class HeartBeatInfo
    {
        /// <summary>
        /// 心跳状态
        /// </summary>
        [JsonPropertyName("heartBeatStatus")]
        public int HeartBeatStatus { get; set; }

        /// <summary>
        /// 心跳时间
        /// </summary>
        [JsonPropertyName("heartBeatTime")]
        public DateTime HeartBeatTime { get; set; }

        public HeartBeatInfo()
        {
            HeartBeatTime = DateTime.Now;
        }

        public HeartBeatInfo(global::CiaiControllerSDK.Models.HeartBeatStatus status) : this()
        {
            HeartBeatStatus = (int)status;
        }

        public static HeartBeatInfo Normal() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.Normal);
        public static HeartBeatInfo DriverAbnormal() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.DriverAbnormal);
        public static HeartBeatInfo DriverOverTime() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.DriverOverTime);
        public static HeartBeatInfo EquipmentAbnormal() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.EquipmentAbnormal);
        public static HeartBeatInfo EquipmentError() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.EquipmentError);
        public static HeartBeatInfo EquipmentOverTime() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.EquipmentOverTime);
        public static HeartBeatInfo Monitoring() => new(global::CiaiControllerSDK.Models.HeartBeatStatus.Monitoring);
    }

    /// <summary>
    /// 心跳状态枚举
    /// </summary>
    public enum HeartBeatStatus
    {
        /// <summary>
        /// 正常
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 驱动异常
        /// </summary>
        DriverAbnormal = 1,

        /// <summary>
        /// 驱动超时
        /// </summary>
        DriverOverTime = 2,

        /// <summary>
        /// 设备异常
        /// </summary>
        EquipmentAbnormal = 3,

        /// <summary>
        /// 设备错误
        /// </summary>
        EquipmentError = 4,

        /// <summary>
        /// 设备超时
        /// </summary>
        EquipmentOverTime = 5,

        /// <summary>
        /// 监控中
        /// </summary>
        Monitoring = 6
    }

    /// <summary>
    /// 注册信息
    /// </summary>
    public class RegisterInfo
    {
        /// <summary>
        /// 基础信息
        /// </summary>
        [JsonPropertyName("basicInfo")]
        public BasicInfo BasicInfo { get; set; }

        /// <summary>
        /// 高级信息
        /// </summary>
        [JsonPropertyName("advancedInfo")]
        public AdvancedInfo AdvancedInfo { get; set; }
    }

    /// <summary>
    /// 基础信息
    /// </summary>
    public class BasicInfo
    {
        /// <summary>
        /// 设备名称
        /// </summary>
        [JsonPropertyName("equipmentName")]
        public string EquipmentName { get; set; }

        /// <summary>
        /// 设备英文名称
        /// </summary>
        [JsonPropertyName("equipmentNameEN")]
        public string EquipmentNameEN { get; set; }

        /// <summary>
        /// 设备型号
        /// </summary>
        [JsonPropertyName("equipmentModel")]
        public string EquipmentModel { get; set; }

        /// <summary>
        /// 设备制造商
        /// </summary>
        [JsonPropertyName("equipmentManufacturer")]
        public string EquipmentManufacturer { get; set; }

        /// <summary>
        /// 驱动作者
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>
        /// 驱动版本
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// 设备类
        /// </summary>
        [JsonPropertyName("equipmentClass")]
        public string EquipmentClass { get; set; }

        /// <summary>
        /// 是否支持急停
        /// </summary>
        [JsonPropertyName("canE_Stop")]
        public int CanEmergencyStop { get; set; }

        /// <summary>
        /// 功能资源数
        /// </summary>
        [JsonPropertyName("functionalResources")]
        public int FunctionalResources { get; set; }

        /// <summary>
        /// 运行时可访问性
        /// </summary>
        [JsonPropertyName("runtimeAccessibility")]
        public int RuntimeAccessibility { get; set; }

        /// <summary>
        /// 是否可并行
        /// </summary>
        [JsonPropertyName("parallelizability")]
        public int Parallelizability { get; set; }

        /// <summary>
        /// 设备图标（Base64）
        /// </summary>
        [JsonPropertyName("equipmentIcon")]
        public string EquipmentIcon { get; set; }

        /// <summary>
        /// 设备类型: 1-核心设备 2-转移设备 3-辅助设备 4-存储设备
        /// </summary>
        [JsonPropertyName("equipmentType")]
        public int EquipmentType { get; set; }
    }

    /// <summary>
    /// 高级信息
    /// </summary>
    public class AdvancedInfo
    {
        /// <summary>
        /// 功能列表
        /// </summary>
        [JsonPropertyName("equipmentFunctions")]
        public List<EquipmentFunction> EquipmentFunctions { get; set; }

        /// <summary>
        /// 状态获取列表
        /// </summary>
        [JsonPropertyName("equipmentGetInfos")]
        public List<EquipmentGetInfo> EquipmentGetInfos { get; set; }

        /// <summary>
        /// 参数设置列表
        /// </summary>
        [JsonPropertyName("equipmentSetInfos")]
        public List<EquipmentSetInfo> EquipmentSetInfos { get; set; }

        /// <summary>
        /// 位置列表
        /// </summary>
        [JsonPropertyName("equipmentNests")]
        public List<EquipmentNest> EquipmentNests { get; set; }

        /// <summary>
        /// 操作列表
        /// </summary>
        [JsonPropertyName("equipmentOperations")]
        public List<EquipmentOperation> EquipmentOperations { get; set; }

        /// <summary>
        /// 进出信息
        /// </summary>
        [JsonPropertyName("equipmentEnterAndExit")]
        public EquipmentEnterAndExit EquipmentEnterAndExit { get; set; }
    }

    /// <summary>
    /// 设备功能信息
    /// </summary>
    public class EquipmentFunction
    {
        [JsonPropertyName("functionName")]
        public string FunctionName { get; set; }

        [JsonPropertyName("functionTitleCN")]
        public string FunctionTitleCN { get; set; }

        [JsonPropertyName("functionTitleEN")]
        public string FunctionTitleEN { get; set; }

        [JsonPropertyName("functionDescription")]
        public string FunctionDescription { get; set; }

        [JsonPropertyName("functionDefaultPeriod")]
        public string FunctionDefaultPeriod { get; set; }

        [JsonPropertyName("functionCategoryCN")]
        public string FunctionCategoryCN { get; set; }

        [JsonPropertyName("functionCategoryEN")]
        public string FunctionCategoryEN { get; set; }

        [JsonPropertyName("iconBlack")]
        public string IconBlack { get; set; }

        [JsonPropertyName("iconWhite")]
        public string IconWhite { get; set; }

        [JsonPropertyName("functionFormJsonStructure")]
        public string FunctionFormJsonStructure { get; set; }
    }

    /// <summary>
    /// 设备状态获取信息
    /// </summary>
    public class EquipmentGetInfo
    {
        [JsonPropertyName("getName")]
        public string GetName { get; set; }

        [JsonPropertyName("getTitleCN")]
        public string GetTitleCN { get; set; }

        [JsonPropertyName("getTitleEN")]
        public string GetTitleEN { get; set; }

        [JsonPropertyName("getType")]
        public new string GetType { get; set; }

        [JsonPropertyName("getUnit")]
        public string GetUnit { get; set; }

        [JsonPropertyName("getDescription")]
        public string Description { get; set; }
    }

    /// <summary>
    /// 设备参数设置信息
    /// </summary>
    public class EquipmentSetInfo
    {
        [JsonPropertyName("setName")]
        public string SetName { get; set; }

        [JsonPropertyName("setTitleCN")]
        public string SetTitleCN { get; set; }

        [JsonPropertyName("setTitleEN")]
        public string SetTitleEN { get; set; }

        [JsonPropertyName("setType")]
        public string SetType { get; set; }

        [JsonPropertyName("setValue")]
        public List<string> SetValue { get; set; }

        [JsonPropertyName("setUnit")]
        public string SetUnit { get; set; }

        [JsonPropertyName("setDescription")]
        public string Description { get; set; }
    }

    /// <summary>
    /// 设备位置信息
    /// </summary>
    public class EquipmentNest
    {
        [JsonPropertyName("nestName")]
        public string NestName { get; set; }

        [JsonPropertyName("labwareType")]
        public string LabwareType { get; set; }

        [JsonPropertyName("nestPostures")]
        public string NestPostures { get; set; }

        [JsonPropertyName("postEnterFormJsonStructure")]
        public string PostEnterFormJsonStructure { get; set; }

        [JsonPropertyName("preEnterFormJsonStructure")]
        public string PreEnterFormJsonStructure { get; set; }

        [JsonPropertyName("postExitFormJsonStructure")]
        public string PostExitFormJsonStructure { get; set; }

        [JsonPropertyName("preExitFormJsonStructure")]
        public string PreExitFormJsonStructure { get; set; }

        [JsonPropertyName("nestAccessibility")]
        public int NestAccessibility { get; set; }

        [JsonPropertyName("nestDescription")]
        public string NestDescription { get; set; }

        [JsonPropertyName("nestHeight")]
        public float NestHeight { get; set; }

        [JsonPropertyName("nestCoordinate")]
        public string NestCoordinate { get; set; }

        [JsonPropertyName("nestColumnOrder")]
        public int NestColumnOrder { get; set; }

        [JsonPropertyName("nestColumnCo")]
        public int? NestColumnCo { get; set; }

        [JsonPropertyName("nestLayerCo")]
        public int? NestLayerCo { get; set; }

        [JsonPropertyName("typeOnly")]
        public int TypeOnly { get; set; }

        [JsonPropertyName("nestIsDestination")]
        public int NestIsDestination { get; set; }

        [JsonPropertyName("transitionNest")]
        public string TransitionNest { get; set; }
    }

    /// <summary>
    /// 设备操作信息
    /// </summary>
    public class EquipmentOperation
    {
        [JsonPropertyName("operationName")]
        public string OperationName { get; set; }

        [JsonPropertyName("operationTitleCN")]
        public string OperationTitleCN { get; set; }

        [JsonPropertyName("operationTitleEN")]
        public string OperationTitleEN { get; set; }

        [JsonPropertyName("operationDescription")]
        public string OperationDescription { get; set; }

        [JsonPropertyName("operationFormJsonStructure")]
        public string OperationFormJsonStructure { get; set; }
    }

    /// <summary>
    /// 设备进出信息
    /// </summary>
    public class EquipmentEnterAndExit
    {
        [JsonPropertyName("enterAndExitName")]
        public string EnterAndExitName { get; set; }

        [JsonPropertyName("enterAndExitTitleCN")]
        public string EnterAndExitTitleCN { get; set; }

        [JsonPropertyName("enterAndExitTitleEN")]
        public string EnterAndExitTitleEN { get; set; }
    }
}

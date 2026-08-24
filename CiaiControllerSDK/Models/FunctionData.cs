using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CiaiControllerSDK.Models
{
    /// <summary>
    /// 功能接口数据
    /// </summary>
    public class FunctionData
    {
        /// <summary>
        /// 功能名称
        /// </summary>
        [JsonPropertyName("functionName")]
        public string FunctionName { get; set; }

        /// <summary>
        /// 指令ID
        /// </summary>
        [JsonPropertyName("instructionId")]
        public string InstructionId { get; set; }

        /// <summary>
        /// 耗材信息
        /// </summary>
        [JsonPropertyName("labwareInfo")]
        public Labware LabwareInfo { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        [JsonPropertyName("equipmentName")]
        public string EquipmentName { get; set; }

        /// <summary>
        /// 位置ID
        /// </summary>
        [JsonPropertyName("nestId")]
        public string NestId { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; }

        /// <summary>
        /// 功能参数
        /// </summary>
        [JsonPropertyName("functionParam")]
        public object FunctionParam { get; set; }
    }

    /// <summary>
    /// 耗材信息
    /// </summary>
    public class Labware
    {
        /// <summary>
        /// 耗材名称
        /// </summary>
        [JsonPropertyName("LabwareName")]
        public string LabwareName { get; set; }

        /// <summary>
        /// 容量
        /// </summary>
        [JsonPropertyName("capacity")]
        public string Capacity { get; set; }

        /// <summary>
        /// 行数
        /// </summary>
        [JsonPropertyName("capacityRow")]
        public int CapacityRow { get; set; }

        /// <summary>
        /// 列数
        /// </summary>
        [JsonPropertyName("capacityColumn")]
        public int CapacityColumn { get; set; }
    }

    /// <summary>
    /// 完成回调数据
    /// </summary>
    public class Finish
    {
        /// <summary>
        /// 完成状态: finish, error
        /// </summary>
        [JsonPropertyName("completion")]
        public string Completion { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [JsonPropertyName("errorMsg")]
        public string ErrorMsg { get; set; }

        /// <summary>
        /// 指令ID
        /// </summary>
        [JsonPropertyName("instructionId")]
        public string InstructionId { get; set; }

        /// <summary>
        /// 位置ID
        /// </summary>
        [JsonPropertyName("nestId")]
        public string NestId { get; set; }

        /// <summary>
        /// 结果输出
        /// </summary>
        [JsonPropertyName("resultOutput")]
        public List<ResultOutput> ResultOutput { get; set; }

        public static Finish Success()
        {
            return new Finish { Completion = "finish" };
        }

        public static Finish Error(string errorMessage)
        {
            return new Finish
            {
                Completion = "error",
                ErrorMsg = errorMessage
            };
        }
    }

    /// <summary>
    /// 结果输出
    /// </summary>
    public class ResultOutput
    {
        /// <summary>
        /// 名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// 结果数据
        /// </summary>
        [JsonPropertyName("resultData")]
        public object ResultData { get; set; }
    }
}

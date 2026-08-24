using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CiaiControllerSDK.Models
{
    /// <summary>
    /// 统一响应结果
    /// </summary>
    public class Result<T>
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        [JsonPropertyName("data")]
        public T Data { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [JsonIgnore]
        public bool IsSuccess => Code == CommonCode.Success;

        public static Result<T> Success()
        {
            return new Result<T>
            {
                Code = CommonCode.Success,
                Message = "Success"
            };
        }

        public static Result<T> Success(T data)
        {
            return new Result<T>
            {
                Code = CommonCode.Success,
                Message = "Success",
                Data = data
            };
        }

        public static Result<T> Success(string message, T data)
        {
            return new Result<T>
            {
                Code = CommonCode.Success,
                Message = message,
                Data = data
            };
        }

        public static Result<T> Failed()
        {
            return new Result<T>
            {
                Code = CommonCode.Failed,
                Message = "Failed"
            };
        }

        public static Result<T> Failed(string message)
        {
            return new Result<T>
            {
                Code = CommonCode.Failed,
                Message = message
            };
        }

        public static Result<T> Failed(string code, string message)
        {
            return new Result<T>
            {
                Code = code,
                Message = message
            };
        }

        public static Result<T> Unauthorized()
        {
            return Failed(CommonCode.Unauthorized, "Unauthorized");
        }

        public static Result<T> Timeout()
        {
            return Failed(CommonCode.Timeout, "Timeout");
        }

        public static Result<T> ServerError(string message = "Server Error")
        {
            return Failed(CommonCode.ServerError, message);
        }

        public static Result<T> ParametersMissing(string message = "Parameters Missing")
        {
            return Failed(CommonCode.ParametersMissing, message);
        }
    }

    /// <summary>
    /// 通用状态码
    /// </summary>
    public static class CommonCode
    {
        public const string Success = "message.common.success";
        public const string Failed = "message.common.failed";
        public const string Unauthorized = "message.common.unauthorized";
        public const string Timeout = "message.common.timeout";
        public const string ServerError = "message.common.server.error";
        public const string ParametersMissing = "message.common.parameters.missing";
    }
}

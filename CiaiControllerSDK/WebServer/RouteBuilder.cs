using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CiaiControllerSDK.Attributes;
using CiaiControllerSDK.Core;

namespace CiaiControllerSDK.WebServer
{
    /// <summary>
    /// 路由信息
    /// </summary>
    public class RouteInfo
    {
        /// <summary>
        /// 路由路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// HTTP方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 处理器类型
        /// </summary>
        public RouteHandlerType HandlerType { get; set; }

        /// <summary>
        /// 关联的方法信息（用于Function/Operation等）
        /// </summary>
        public MethodInfo MethodInfo { get; set; }

        /// <summary>
        /// 关联的特性
        /// </summary>
        public Attribute Attribute { get; set; }
    }

    /// <summary>
    /// 路由处理器类型
    /// </summary>
    public enum RouteHandlerType
    {
        Info,
        HeartBeat,
        Function,
        Operation,
        Set,
        Get,
        EnterAndExit
    }

    /// <summary>
    /// 路由构建器 - 从驱动类扫描并构建API路由
    /// </summary>
    public static class RouteBuilder
    {
        /// <summary>
        /// 标准API端点路径
        /// </summary>
        public static class Endpoints
        {
            public const string Info = "/Info";
            public const string HeartBeat = "/HeartBeat";
            public const string Function = "/Function";
            public const string Operation = "/Operation";
            public const string Set = "/Set";
            public const string Get = "/Get";
            public const string EnterAndExit = "/EnterAndExit";

            public static bool IsKnown(string path)
            {
                return path == Info || path == HeartBeat || path == Function || path == Operation ||
                       path == Set || path == Get || path == EnterAndExit;
            }
        }

        /// <summary>
        /// 从驱动实例构建路由表
        /// </summary>
        /// <param name="driver">驱动实例</param>
        /// <returns>路由信息列表</returns>
        public static List<RouteInfo> BuildRoutes(DeviceDriverBase driver)
        {
            var routes = new List<RouteInfo>();
            var driverType = driver.GetType();

            // 添加标准端点
            routes.Add(new RouteInfo
            {
                Path = Endpoints.Info,
                Method = "GET",
                HandlerType = RouteHandlerType.Info
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.HeartBeat,
                Method = "GET",
                HandlerType = RouteHandlerType.HeartBeat
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.Function,
                Method = "POST",
                HandlerType = RouteHandlerType.Function
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.Operation,
                Method = "POST",
                HandlerType = RouteHandlerType.Operation
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.Set,
                Method = "POST",
                HandlerType = RouteHandlerType.Set
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.Get,
                Method = "GET",
                HandlerType = RouteHandlerType.Get
            });

            routes.Add(new RouteInfo
            {
                Path = Endpoints.EnterAndExit,
                Method = "POST",
                HandlerType = RouteHandlerType.EnterAndExit
            });

            return routes;
        }

        /// <summary>
        /// 获取驱动的方法映射
        /// </summary>
        /// <param name="driver">驱动实例</param>
        /// <returns>方法名称到方法信息的映射</returns>
        public static Dictionary<string, MethodInfo> GetFunctionMethods(DeviceDriverBase driver)
        {
            var result = new Dictionary<string, MethodInfo>();
            var methods = driver.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DeviceFunctionAttribute>();
                if (attr != null)
                {
                    result[attr.Name] = method;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取操作方法映射
        /// </summary>
        public static Dictionary<string, MethodInfo> GetOperationMethods(DeviceDriverBase driver)
        {
            var result = new Dictionary<string, MethodInfo>();
            var methods = driver.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DeviceOperationAttribute>();
                if (attr != null)
                {
                    result[attr.Name] = method;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取设置方法映射
        /// </summary>
        public static Dictionary<string, MethodInfo> GetSetMethods(DeviceDriverBase driver)
        {
            var result = new Dictionary<string, MethodInfo>();
            var methods = driver.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DeviceSetAttribute>();
                if (attr != null)
                {
                    result[attr.Name] = method;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取状态获取方法映射
        /// </summary>
        public static Dictionary<string, MethodInfo> GetGetMethods(DeviceDriverBase driver)
        {
            var result = new Dictionary<string, MethodInfo>();
            var methods = driver.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DeviceGetAttribute>();
                if (attr != null)
                {
                    result[attr.Name] = method;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取进出方法映射
        /// </summary>
        public static Dictionary<string, MethodInfo> GetEnterExitMethods(DeviceDriverBase driver)
        {
            var result = new Dictionary<string, MethodInfo>();
            var methods = driver.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DeviceEnterExitAttribute>();
                if (attr != null)
                {
                    result[attr.Name] = method;
                }
            }

            return result;
        }
    }
}

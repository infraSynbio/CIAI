using System;
using System.Collections.Concurrent;
using CiaiControllerSDK.Core;

namespace CiaiControllerSDK.Services
{
    /// <summary>
    /// 设备驱动工厂 - 用于创建和管理驱动实例
    /// </summary>
    public class DeviceDriverFactory
    {
        private static readonly ConcurrentDictionary<string, DeviceDriverBase> _drivers = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 创建设备驱动实例
        /// </summary>
        /// <typeparam name="T">驱动类型</typeparam>
        /// <param name="configuration">设备配置</param>
        /// <returns>驱动实例</returns>
        public static T CreateDriver<T>(DeviceConfiguration configuration) where T : DeviceDriverBase
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var driverType = typeof(T);
            var configurationConstructor = driverType.GetConstructor(new[] { typeof(DeviceConfiguration) });
            if (configurationConstructor != null)
                return (T)configurationConstructor.Invoke(new object[] { configuration });

            var parameterlessConstructor = driverType.GetConstructor(Type.EmptyTypes);
            if (parameterlessConstructor == null)
                throw new InvalidOperationException(
                    $"无法创建驱动实例 {driverType.Name}：需要公共无参构造函数或DeviceConfiguration构造函数");

            var driver = (T)parameterlessConstructor.Invoke(null);
            driver.ApplyConfiguration(configuration);
            return driver;
        }

        /// <summary>
        /// 创建并注册设备驱动
        /// </summary>
        public static T CreateAndRegisterDriver<T>(DeviceConfiguration configuration) where T : DeviceDriverBase
        {
            var driver = CreateDriver<T>(configuration);
            RegisterDriver(configuration.DeviceId, driver);
            return driver;
        }

        /// <summary>
        /// 注册驱动实例
        /// </summary>
        public static void RegisterDriver(string deviceId, DeviceDriverBase driver)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentNullException(nameof(deviceId));

            if (driver == null)
                throw new ArgumentNullException(nameof(driver));

            _drivers[deviceId] = driver;
        }

        /// <summary>
        /// 获取已注册的驱动
        /// </summary>
        public static DeviceDriverBase GetDriver(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentNullException(nameof(deviceId));

            _drivers.TryGetValue(deviceId, out var driver);
            return driver;
        }

        /// <summary>
        /// 获取已注册的驱动（泛型版本）
        /// </summary>
        public static T GetDriver<T>(string deviceId) where T : DeviceDriverBase
        {
            return GetDriver(deviceId) as T;
        }

        /// <summary>
        /// 移除已注册的驱动
        /// </summary>
        public static async System.Threading.Tasks.Task UnregisterDriverAsync(string deviceId)
        {
            if (_drivers.TryRemove(deviceId, out var driver))
            {
                await driver.DisposeAsync();
            }
        }

        /// <summary>
        /// 清除所有已注册的驱动
        /// </summary>
        public static async System.Threading.Tasks.Task ClearAllAsync()
        {
            foreach (var kvp in _drivers)
            {
                await kvp.Value.DisposeAsync();
            }
            _drivers.Clear();
        }
    }
}

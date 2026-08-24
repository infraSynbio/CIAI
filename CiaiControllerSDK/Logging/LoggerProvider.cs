using Microsoft.Extensions.Logging;

namespace CiaiControllerSDK.Logging
{
    /// <summary>
    /// 日志管理器 - 提供全局日志工厂和静态日志访问
    /// </summary>
    public static class LoggerProvider
    {
        private static ILoggerFactory _factory;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取当前日志工厂
        /// </summary>
        public static ILoggerFactory Factory
        {
            get
            {
                if (_factory == null)
                {
                    lock (_lock)
                    {
                        if (_factory == null)
                        {
                            // 默认使用 ConsoleLogger
                            _factory = LoggerFactory.Create(builder =>
                            {
                                builder.AddConsole();
                                builder.SetMinimumLevel(LogLevel.Debug);
                            });
                        }
                    }
                }
                return _factory;
            }
        }

        /// <summary>
        /// 设置自定义日志工厂
        /// </summary>
        /// <param name="factory">日志工厂实例</param>
        /// <remarks>
        /// 调用方可通过此方法注入自定义的日志实现（如 Serilog、NLog 等）
        /// 注意：调用方需自行管理工厂的生命周期，不要在使用期间 dispose
        /// 示例：
        /// <code>
        /// var loggerFactory = LoggerFactory.Create(builder =>
        ///     builder.AddSerilog(new LoggerConfiguration()
        ///         .WriteTo.File("log.txt")
        ///         .CreateLogger()));
        /// LoggerProvider.SetLoggerFactory(loggerFactory);
        /// </code>
        /// </remarks>
        public static void SetLoggerFactory(ILoggerFactory factory)
        {
            lock (_lock)
            {
                // 不释放旧的工厂，由调用方管理生命周期
                _factory = factory;
            }
        }

        /// <summary>
        /// 创建类型化日志器
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns>日志器实例</returns>
        public static ILogger CreateLogger<T>()
        {
            return Factory.CreateLogger<T>();
        }

        /// <summary>
        /// 创建命名日志器
        /// </summary>
        /// <param name="categoryName">类别名称</param>
        /// <returns>日志器实例</returns>
        public static ILogger CreateLogger(string categoryName)
        {
            return Factory.CreateLogger(categoryName);
        }

        /// <summary>
        /// 重置为默认的 ConsoleLogger
        /// </summary>
        public static void ResetToDefault()
        {
            lock (_lock)
            {
                // 不释放旧的工厂，由调用方管理生命周期
                _factory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            }
        }
    }
}

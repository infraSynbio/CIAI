using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CiaiControllerSDK.Icons
{
    /// <summary>
    /// 图标辅助类 - 提供默认图标和图标加载功能
    /// </summary>
    public static class IconHelper
    {
        /// <summary>
        /// 图标文件夹路径（可由外部设置）
        /// 默认为运行目录下的icon文件夹
        /// </summary>
        public static string IconFolderPath { get; set; }

        /// <summary>
        /// 默认设备图标（从嵌入资源加载，带data URI前缀）
        /// </summary>
        public static readonly string DefaultEquipmentIcon = LoadEmbeddedIcon("设备默认图片.png");

        /// <summary>
        /// 默认组件图标 - 黑色版本（从嵌入资源加载，带data URI前缀）
        /// </summary>
        public static readonly string DefaultFunctionIconBlack = LoadEmbeddedIcon("icon_组件_默认图标_黑色版.png");

        /// <summary>
        /// 默认组件图标 - 白色版本（从嵌入资源加载，带data URI前缀）
        /// </summary>
        public static readonly string DefaultFunctionIconWhite = LoadEmbeddedIcon("icon_组件_默认图标_白色版.png");

        /// <summary>
        /// 静态构造函数 - 初始化默认图标路径
        /// </summary>
        static IconHelper()
        {
            // 默认图标路径搜索顺序：
            // 1. 运行目录/icon
            // 2. 当前工作目录/icon
            // 3. 项目根目录/icon（开发时）
            // 4. SDK所在目录/icon

            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon"),
                Path.Combine(Directory.GetCurrentDirectory(), "icon"),
                FindProjectIconPath(),
                Path.Combine(GetSdkDirectory(), "icon")
            };

            foreach (var path in searchPaths)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    IconFolderPath = path;
                    break;
                }
            }
        }

        /// <summary>
        /// 从嵌入资源加载图标
        /// </summary>
        /// <param name="fileName">资源文件名</param>
        /// <returns>带data URI前缀的Base64编码图片字符串</returns>
        private static string LoadEmbeddedIcon(string fileName)
        {
            try
            {
                var assembly = typeof(IconHelper).Assembly;
                // 嵌入资源名称格式：程序集名.icon.文件名
                var resourceName = $"CiaiControllerSDK.icon.{fileName}";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    // 尝试查找正确的资源名称
                    var resourceNames = assembly.GetManifestResourceNames();
                    foreach (var name in resourceNames)
                    {
                        if (name.EndsWith(fileName))
                        {
                            using var correctStream = assembly.GetManifestResourceStream(name);
                            return LoadFromStream(correctStream, fileName);
                        }
                    }
                    return string.Empty;
                }

                return LoadFromStream(stream, fileName);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 从流中加载图标并转换为Base64
        /// </summary>
        private static string LoadFromStream(Stream stream, string fileName)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = GetMimeType(fileName);
            return $"data:{mimeType};base64,{base64}";
        }

        /// <summary>
        /// 查找项目根目录的icon文件夹（开发时使用）
        /// </summary>
        private static string FindProjectIconPath()
        {
            var currentDir = Directory.GetCurrentDirectory();

            // 向上查找，直到找到icon文件夹或到达根目录
            var dir = currentDir;
            for (int i = 0; i < 10; i++)
            {
                var iconPath = Path.Combine(dir, "icon");
                if (Directory.Exists(iconPath))
                {
                    return iconPath;
                }

                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            return null;
        }

        /// <summary>
        /// 获取SDK所在目录
        /// </summary>
        private static string GetSdkDirectory()
        {
            // 尝试找到SDK DLL所在目录
            var sdkAssembly = typeof(IconHelper).Assembly;
            var location = sdkAssembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                return Path.GetDirectoryName(location);
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// 从文件加载图标并转换为Base64（带data URI前缀）
        /// </summary>
        /// <param name="filePath">图片文件路径</param>
        /// <returns>带data URI前缀的Base64编码图片字符串，如 data:image/png;base64,xxxxx</returns>
        public static string LoadIconFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            if (!File.Exists(filePath))
                return null;

            var bytes = File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = GetMimeType(filePath);
            return $"data:{mimeType};base64,{base64}";
        }

        /// <summary>
        /// 根据文件扩展名获取MIME类型
        /// </summary>
        private static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                _ => "image/png" // 默认使用png
            };
        }

        /// <summary>
        /// 尝试从文件加载图标，失败返回null
        /// </summary>
        /// <param name="filePath">图片文件路径</param>
        /// <param name="base64">输出的Base64字符串</param>
        /// <returns>是否成功加载</returns>
        public static bool TryLoadIconFromFile(string filePath, out string base64)
        {
            base64 = null;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return false;

                base64 = LoadIconFromFile(filePath);
                return !string.IsNullOrEmpty(base64);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取图标完整路径
        /// 搜索顺序：
        /// 1. 如果是绝对路径且存在，直接返回
        /// 2. IconFolderPath + 文件名
        /// 3. 运行目录/icon + 文件名
        /// 4. 当前目录/icon + 文件名
        /// </summary>
        /// <param name="iconFileName">图标文件名或完整路径</param>
        /// <returns>完整路径，找不到返回null</returns>
        public static string GetIconPath(string iconFileName)
        {
            if (string.IsNullOrEmpty(iconFileName))
                return null;

            // 如果是绝对路径，直接检查是否存在
            if (Path.IsPathRooted(iconFileName))
            {
                if (File.Exists(iconFileName))
                    return iconFileName;
                return null;
            }

            // 搜索路径列表
            var searchPaths = new List<string>();

            // 1. 配置的IconFolderPath
            if (!string.IsNullOrEmpty(IconFolderPath))
            {
                searchPaths.Add(IconFolderPath);
            }

            // 2. 运行目录/icon
            searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon"));

            // 3. 当前目录/icon
            searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "icon"));

            // 4. 项目根目录/icon（开发时）
            var projectIconPath = FindProjectIconPath();
            if (!string.IsNullOrEmpty(projectIconPath))
            {
                searchPaths.Add(projectIconPath);
            }

            // 遍历搜索
            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.Combine(searchPath, iconFileName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// 加载图标（自动搜索icon文件夹，失败则返回null）
        /// </summary>
        /// <param name="iconFileName">图标文件名</param>
        /// <returns>带data URI前缀的Base64编码图片字符串，找不到返回null</returns>
        public static string LoadIcon(string iconFileName)
        {
            var path = GetIconPath(iconFileName);
            if (path != null && File.Exists(path))
            {
                return LoadIconFromFile(path);
            }
            return null;
        }
    }
}

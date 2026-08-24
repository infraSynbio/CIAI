package com.ciai.controller.sdk.icons;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Base64;

/**
 * 图标辅助类
 */
public final class IconHelper {

    private static final Logger logger = LoggerFactory.getLogger(IconHelper.class);

    private static String iconFolderPath = "icon";

    // 默认设备图标 (从classpath加载，带data URI前缀)
    private static final String DEFAULT_EQUIPMENT_ICON = loadDefaultIconFromResource("设备默认图片.png");

    // 默认功能图标黑色 (从classpath加载，带data URI前缀)
    private static final String DEFAULT_FUNCTION_ICON_BLACK = loadDefaultIconFromResource("icon_组件_默认图标_黑色版.png");

    // 默认功能图标白色 (从classpath加载，带data URI前缀)
    private static final String DEFAULT_FUNCTION_ICON_WHITE = loadDefaultIconFromResource("icon_组件_默认图标_白色版.png");

    private IconHelper() {
        // 私有构造函数，防止实例化
    }

    /**
     * 获取图标文件夹路径
     */
    public static String getIconFolderPath() {
        return iconFolderPath;
    }

    /**
     * 设置图标文件夹路径
     */
    public static void setIconFolderPath(String path) {
        iconFolderPath = path;
    }

    /**
     * 获取默认设备图标
     */
    public static String getDefaultEquipmentIcon() {
        return DEFAULT_EQUIPMENT_ICON;
    }

    /**
     * 获取默认功能图标黑色
     */
    public static String getDefaultFunctionIconBlack() {
        return DEFAULT_FUNCTION_ICON_BLACK;
    }

    /**
     * 获取默认功能图标白色
     */
    public static String getDefaultFunctionIconWhite() {
        return DEFAULT_FUNCTION_ICON_WHITE;
    }

    /**
     * 从文件加载图标（带data URI前缀）
     */
    public static String loadIconFromFile(String filePath) {
        try {
            Path path = Paths.get(filePath);
            if (!Files.exists(path)) {
                logger.warn("Icon file not found: {}", filePath);
                return null;
            }

            byte[] bytes = Files.readAllBytes(path);
            String base64 = Base64.getEncoder().encodeToString(bytes);
            String mimeType = getMimeType(filePath);
            return "data:" + mimeType + ";base64," + base64;
        } catch (IOException e) {
            logger.error("Failed to load icon from file: {}", filePath, e);
            return null;
        }
    }

    /**
     * 获取图标路径
     */
    public static String getIconPath(String iconFileName) {
        return Paths.get(iconFolderPath, iconFileName).toString();
    }

    /**
     * 加载图标（带data URI前缀）
     * 先尝试从外部文件加载，再尝试从classpath加载
     */
    public static String loadIcon(String iconFileName) {
        // 首先尝试从文件系统加载
        String filePath = getIconPath(iconFileName);
        Path path = Paths.get(filePath);

        if (Files.exists(path)) {
            return loadIconFromFile(filePath);
        }

        // 尝试从classpath加载
        String result = loadIconFromResource(iconFileName);
        if (result != null) {
            return result;
        }

        logger.warn("Icon not found: {}", iconFileName);
        return null;
    }

    /**
     * 从classpath加载图标
     */
    private static String loadIconFromResource(String fileName) {
        try (InputStream is = IconHelper.class.getClassLoader()
                .getResourceAsStream(iconFolderPath + "/" + fileName)) {
            if (is != null) {
                ByteArrayOutputStream buffer = new ByteArrayOutputStream();
                byte[] data = new byte[4096];
                int nRead;
                while ((nRead = is.read(data, 0, data.length)) != -1) {
                    buffer.write(data, 0, nRead);
                }
                byte[] bytes = buffer.toByteArray();
                String base64 = Base64.getEncoder().encodeToString(bytes);
                String mimeType = getMimeType(fileName);
                return "data:" + mimeType + ";base64," + base64;
            }
        } catch (IOException e) {
            logger.error("Failed to load icon from classpath: {}", fileName, e);
        }
        return null;
    }

    /**
     * 从classpath加载默认图标（启动时加载，失败则使用SVG备用图标）
     */
    private static String loadDefaultIconFromResource(String fileName) {
        String result = loadIconFromResource(fileName);
        if (result != null) {
            return result;
        }

        // 如果加载失败，返回一个简单的SVG默认图标
        logger.warn("Failed to load default icon from resource: {}, using SVG fallback", fileName);
        String svgContent;
        if (fileName.contains("黑色")) {
            svgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"black\">" +
                    "<path d=\"M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5\"/>" +
                    "</svg>";
        } else if (fileName.contains("白色")) {
            svgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"white\">" +
                    "<path d=\"M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5\"/>" +
                    "</svg>";
        } else {
            svgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\">" +
                    "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\" stroke=\"currentColor\" stroke-width=\"2\" fill=\"none\"/>" +
                    "<circle cx=\"12\" cy=\"12\" r=\"4\"/>" +
                    "</svg>";
        }
        String base64 = Base64.getEncoder().encodeToString(svgContent.getBytes());
        return "data:image/svg+xml;base64," + base64;
    }

    /**
     * 根据文件扩展名获取MIME类型
     */
    private static String getMimeType(String fileName) {
        if (fileName == null) {
            return "image/png";
        }
        String extension = "";
        int dotIndex = fileName.lastIndexOf('.');
        if (dotIndex > 0) {
            extension = fileName.substring(dotIndex).toLowerCase();
        }
        switch (extension) {
            case ".png":
                return "image/png";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".gif":
                return "image/gif";
            case ".bmp":
                return "image/bmp";
            case ".svg":
                return "image/svg+xml";
            case ".webp":
                return "image/webp";
            case ".ico":
                return "image/x-icon";
            default:
                return "image/png";
        }
    }
}

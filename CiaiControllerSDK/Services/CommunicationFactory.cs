using System;
using System.IO.Ports;
using System.Text;
using CiaiControllerSDK.Communication;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Interfaces;

namespace CiaiControllerSDK.Services
{
    /// <summary>
    /// 从统一设备配置创建SDK内置通信对象。
    /// </summary>
    public static class CommunicationFactory
    {
        public static ICommunication Create(DeviceConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            ValidateCommon(configuration);
            return configuration.CommunicationType switch
            {
                CommunicationType.TCP => CreateTcp(configuration),
                CommunicationType.HTTP => CreateHttp(configuration),
                CommunicationType.Serial => CreateSerial(configuration),
                CommunicationType.DLL => null,
                _ => throw new NotSupportedException(
                    $"不支持的通信类型: {configuration.CommunicationType}")
            };
        }

        /// <summary>
        /// 配置是否包含创建内置通信对象所需的最小字段。
        /// </summary>
        public static bool CanCreate(DeviceConfiguration configuration)
        {
            if (configuration == null)
                return false;
            return configuration.CommunicationType switch
            {
                CommunicationType.TCP => !string.IsNullOrWhiteSpace(configuration.Host) && configuration.Port > 0,
                CommunicationType.HTTP => !string.IsNullOrWhiteSpace(configuration.BaseUrl),
                CommunicationType.Serial => !string.IsNullOrWhiteSpace(configuration.SerialPort),
                CommunicationType.DLL => true,
                _ => false
            };
        }

        public static void Validate(DeviceConfiguration configuration)
        {
            var communication = Create(configuration);
            (communication as IDisposable)?.Dispose();
        }

        private static ICommunication CreateTcp(DeviceConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.Host))
                throw new ArgumentException("TCP通信必须配置device.tcp.host");
            if (configuration.Port <= 0 || configuration.Port > 65535)
                throw new ArgumentException("TCP通信必须配置1到65535之间的device.tcp.port");

            return new TcpCommunication(
                configuration.Host,
                configuration.Port,
                configuration.ConnectionTimeout,
                configuration.ReadTimeout,
                configuration.WriteTimeout);
        }

        private static ICommunication CreateHttp(DeviceConfiguration configuration)
        {
            if (!Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("HTTP通信必须配置有效的http/https device.http.baseUrl");
            }

            return new HttpCommunication(configuration.BaseUrl, configuration.ConnectionTimeout);
        }

        private static ICommunication CreateSerial(DeviceConfiguration configuration)
        {
            return new SerialCommunication(
                configuration.SerialPort,
                configuration.BaudRate,
                ParseParity(configuration.Parity),
                configuration.DataBits,
                ParseStopBits(configuration.StopBits),
                configuration.ReadTimeout,
                configuration.WriteTimeout,
                ParseEncoding(configuration.Encoding),
                ParseHandshake(configuration.FlowControl),
                configuration.DtrEnable,
                configuration.RtsEnable,
                configuration.DiscardInputBeforeWrite);
        }

        private static void ValidateCommon(DeviceConfiguration configuration)
        {
            if (configuration.ConnectionTimeout <= 0)
                throw new ArgumentException("ConnectionTimeout必须大于0");
            if (configuration.ReadTimeout <= 0)
                throw new ArgumentException("ReadTimeout必须大于0");
            if (configuration.WriteTimeout <= 0)
                throw new ArgumentException("WriteTimeout必须大于0");
            if (configuration.DeviceCallResources <= 0)
                throw new ArgumentException("DeviceCallResources必须大于0");
            if (configuration.DeviceCallTimeout <= 0)
                throw new ArgumentException("DeviceCallTimeout必须大于0");
        }

        private static Parity ParseParity(string value)
        {
            return (value ?? "none").Trim().ToLowerInvariant() switch
            {
                "none" => Parity.None,
                "odd" => Parity.Odd,
                "even" => Parity.Even,
                "mark" => Parity.Mark,
                "space" => Parity.Space,
                _ => throw new ArgumentException($"不支持的串口校验方式: {value}")
            };
        }

        private static StopBits ParseStopBits(double value)
        {
            if (Math.Abs(value - 1) < 0.001) return StopBits.One;
            if (Math.Abs(value - 1.5) < 0.001) return StopBits.OnePointFive;
            if (Math.Abs(value - 2) < 0.001) return StopBits.Two;
            throw new ArgumentException($"不支持的串口停止位: {value}，仅支持1、1.5、2");
        }

        private static Encoding ParseEncoding(string value)
        {
            return (value ?? "utf-8").Trim().ToLowerInvariant() switch
            {
                "utf-8" or "utf8" => Encoding.UTF8,
                "ascii" => Encoding.ASCII,
                "unicode" or "utf-16" or "utf16" => Encoding.Unicode,
                "latin1" or "iso-8859-1" => Encoding.Latin1,
                _ => throw new ArgumentException(
                    $"不支持的内置串口编码: {value}。二进制协议请使用byte[]")
            };
        }

        private static Handshake ParseHandshake(string value)
        {
            return (value ?? "none").Trim().ToLowerInvariant() switch
            {
                "none" => Handshake.None,
                "xonxoff" or "xon/xoff" or "software" => Handshake.XOnXOff,
                "rtscts" or "rts/cts" or "hardware" => Handshake.RequestToSend,
                "rtscts+xonxoff" or "both" => Handshake.RequestToSendXOnXOff,
                _ => throw new ArgumentException($"不支持的串口流控方式: {value}")
            };
        }
    }
}

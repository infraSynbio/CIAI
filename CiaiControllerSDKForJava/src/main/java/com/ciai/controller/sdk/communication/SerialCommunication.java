package com.ciai.controller.sdk.communication;

import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.interface_.IFramedCommunication;
import com.fazecast.jSerialComm.SerialPort;
import com.ciai.controller.sdk.logging.LoggerProvider;
import org.slf4j.Logger;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.Charset;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.ReentrantLock;

/**
 * 串口通信实现 (基于 jSerialComm)
 */
public class SerialCommunication implements IFramedCommunication, AutoCloseable {

    private static final Logger logger = LoggerProvider.createLogger(SerialCommunication.class);

    private final String portName;
    private final int baudRate;
    private final int dataBits;
    private final int stopBits;
    private final int parity;
    private final int readTimeout;
    private final int writeTimeout;
    private final Charset encoding;
    private final int flowControl;
    private final boolean dtrEnable;
    private final boolean rtsEnable;
    private final boolean discardInputBeforeWrite;
    private final ReentrantLock transactionLock = new ReentrantLock(true);

    private SerialPort serialPort; // jSerialComm serial port object
    private InputStream inputStream;
    private OutputStream outputStream;
    private final ExecutorService executor = Executors.newFixedThreadPool(2);
    private boolean connected = false;

    // Parity constants
    public static final int PARITY_NONE = 0;
    public static final int PARITY_ODD = 1;
    public static final int PARITY_EVEN = 2;
    public static final int PARITY_MARK = 3;
    public static final int PARITY_SPACE = 4;

    // Stop bits constants
    public static final int STOPBITS_ONE = 1;
    public static final int STOPBITS_ONE_POINT_FIVE = 3;
    public static final int STOPBITS_TWO = 2;

    public SerialCommunication(String portName) {
        this(portName, 9600);
    }

    public SerialCommunication(String portName, int baudRate) {
        this(portName, baudRate, 8, STOPBITS_ONE, PARITY_NONE, 5000);
    }

    public SerialCommunication(String portName, int baudRate, int dataBits, int stopBits, int parity, int timeout) {
        this(portName, baudRate, dataBits, stopBits, parity, timeout, timeout, Charset.forName("UTF-8"));
    }

    public SerialCommunication(String portName, int baudRate, int dataBits, int stopBits, int parity,
                               int readTimeout, int writeTimeout, Charset encoding) {
        this(portName, baudRate, dataBits, stopBits, parity, readTimeout, writeTimeout, encoding,
                SerialPort.FLOW_CONTROL_DISABLED, false, false, false);
    }

    public SerialCommunication(String portName, int baudRate, int dataBits, int stopBits, int parity,
                               int readTimeout, int writeTimeout, Charset encoding, int flowControl,
                               boolean dtrEnable, boolean rtsEnable, boolean discardInputBeforeWrite) {
        if (portName == null || portName.trim().isEmpty()) {
            throw new IllegalArgumentException("Serial port name is required");
        }
        if (baudRate <= 0 || dataBits < 5 || dataBits > 8) {
            throw new IllegalArgumentException("Invalid serial port parameters");
        }
        if (readTimeout <= 0 || writeTimeout <= 0) {
            throw new IllegalArgumentException("Serial timeouts must be greater than zero");
        }
        this.portName = portName;
        this.baudRate = baudRate;
        this.dataBits = dataBits;
        this.stopBits = stopBits;
        this.parity = parity;
        this.readTimeout = readTimeout;
        this.writeTimeout = writeTimeout;
        this.encoding = encoding == null ? Charset.forName("UTF-8") : encoding;
        this.flowControl = flowControl;
        this.dtrEnable = dtrEnable;
        this.rtsEnable = rtsEnable;
        this.discardInputBeforeWrite = discardInputBeforeWrite;
    }

    @Override
    public boolean isConnected() {
        return connected && inputStream != null && outputStream != null;
    }

    @Override
    public CompletableFuture<Boolean> connectAsync() {
        return CompletableFuture.supplyAsync(this::connect, executor);
    }

    @Override
    public CompletableFuture<Void> disconnectAsync() {
        return CompletableFuture.runAsync(this::disconnect, executor);
    }

    @Override
    public CompletableFuture<Boolean> sendAsync(byte[] data) {
        return CompletableFuture.supplyAsync(() -> send(data), executor);
    }

    @Override
    public CompletableFuture<byte[]> receiveAsync() {
        return CompletableFuture.supplyAsync(this::receive, executor);
    }

    @Override
    public CompletableFuture<byte[]> sendAndReceiveAsync(byte[] data) {
        return CompletableFuture.supplyAsync(() -> sendAndReceive(data), executor);
    }

    @Override
    public CompletableFuture<byte[]> readExactAsync(int length) {
        return CompletableFuture.supplyAsync(() -> readExact(length), executor);
    }

    @Override
    public CompletableFuture<byte[]> readUntilAsync(byte endByte, int maxLength) {
        return CompletableFuture.supplyAsync(() -> readUntil(endByte, maxLength), executor);
    }

    @Override
    public CompletableFuture<byte[]> sendAndReadExactAsync(byte[] data, int length) {
        return CompletableFuture.supplyAsync(() -> sendAndReadExact(data, length), executor);
    }

    @Override
    public CompletableFuture<byte[]> sendAndReadUntilAsync(byte[] data, byte endByte, int maxLength) {
        return CompletableFuture.supplyAsync(
                () -> sendAndReadUntil(data, endByte, maxLength), executor);
    }

    @Override public CompletableFuture<byte[]> readUntilAsync(byte[] delimiter, int maxLength) {
        return CompletableFuture.supplyAsync(() -> readUntil(delimiter, maxLength), executor);
    }
    @Override public CompletableFuture<byte[]> sendAndReadUntilAsync(byte[] data, byte[] delimiter, int maxLength) {
        return CompletableFuture.supplyAsync(() -> sendAndReadUntil(data, delimiter, maxLength), executor);
    }

    @Override
    public boolean connect() {
        transactionLock.lock();
        logger.info("Attempting to connect to serial port: {} at {} baud, {} data bits, {} stop bits, parity {}",
                portName, baudRate, dataBits, stopBits, parity);

        try {
            disconnectCore();
            serialPort = SerialPort.getCommPort(portName);
            serialPort.setComPortParameters(baudRate, dataBits, stopBits, parity);
            serialPort.setFlowControl(flowControl);
            serialPort.setComPortTimeouts(
                    SerialPort.TIMEOUT_READ_SEMI_BLOCKING | SerialPort.TIMEOUT_WRITE_BLOCKING,
                    readTimeout, writeTimeout);

            if (serialPort.openPort()) {
                if (dtrEnable) serialPort.setDTR(); else serialPort.clearDTR();
                if (rtsEnable) serialPort.setRTS(); else serialPort.clearRTS();
                inputStream = serialPort.getInputStream();
                outputStream = serialPort.getOutputStream();
                connected = true;
                logger.info("Serial port {} connected successfully", portName);
                return true;
            } else {
                logger.error("Failed to open serial port {}", portName);
                return false;
            }
        } catch (Exception e) {
            disconnectCore();
            logger.error("Serial port connection failed: {}", portName, e);
            return false;
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public void disconnect() {
        transactionLock.lock();
        try {
            disconnectCore();
            logger.info("Serial port {} disconnected", portName);
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public boolean send(byte[] data) {
        transactionLock.lock();
        try {
            return sendCore(data);
        } finally {
            transactionLock.unlock();
        }
    }

    private boolean sendCore(byte[] data) {
        if (!isConnected() || outputStream == null) {
            logger.error("Serial port not connected, cannot send");
            return false;
        }
        try {
            if (discardInputBeforeWrite) serialPort.flushIOBuffers();
            outputStream.write(data);
            outputStream.flush();
            logger.debug("Serial sent {} bytes", data.length);
            return true;
        } catch (IOException e) {
            logger.error("Serial send error", e);
            return false;
        }
    }

    @Override
    public byte[] receive() {
        transactionLock.lock();
        try {
            return receiveCore();
        } finally {
            transactionLock.unlock();
        }
    }

    private byte[] receiveCore() {
        if (!isConnected() || inputStream == null) {
            logger.error("Serial port not connected, cannot receive");
            return null;
        }
        try {
            byte[] buffer = new byte[4096];
            int bytesRead = inputStream.read(buffer);
            if (bytesRead > 0) {
                byte[] result = new byte[bytesRead];
                System.arraycopy(buffer, 0, result, 0, bytesRead);
                logger.debug("Serial received {} bytes", bytesRead);
                return result;
            }
            return null;
        } catch (IOException e) {
            logger.error("Serial receive error", e);
            return null;
        }
    }

    @Override
    public byte[] sendAndReceive(byte[] data) {
        transactionLock.lock();
        try {
            if (sendCore(data)) {
                return receiveCore();
            }
            return null;
        } finally {
            transactionLock.unlock();
        }
    }

    /**
     * 发送字符串命令
     */
    public CompletableFuture<Boolean> sendCommandAsync(String command) {
        return CompletableFuture.supplyAsync(() -> sendCommand(command), executor);
    }

    /**
     * 发送字符串命令
     */
    public boolean sendCommand(String command) {
        return send(command.getBytes(encoding));
    }

    /**
     * 接收字符串响应
     */
    public CompletableFuture<String> receiveResponseAsync() {
        return CompletableFuture.supplyAsync(this::receiveResponse, executor);
    }

    /**
     * 接收字符串响应
     */
    public String receiveResponse() {
        byte[] data = receive();
        return data != null ? new String(data, encoding) : null;
    }

    /**
     * 发送并接收字符串
     */
    public CompletableFuture<String> sendAndReceiveStringAsync(String command) {
        return CompletableFuture.supplyAsync(() -> sendAndReceiveString(command), executor);
    }

    /**
     * 发送并接收字符串
     */
    public String sendAndReceiveString(String command) {
        transactionLock.lock();
        try {
            byte[] result = sendAndReceive(command.getBytes(encoding));
            return result != null ? new String(result, encoding) : null;
        } finally {
            transactionLock.unlock();
        }
    }

    /**
     * 循环读取串口响应直到帧尾标识或超时（参照原封膜机TCPClient.Read实现）
     * @param frameEndByte 帧尾标识字节(如0xFF)
     * @param timeoutMs 超时毫秒
     * @return 完整响应字节数组，超时返回null
     */
    public byte[] readResponse(int frameEndByte, int timeoutMs) {
        transactionLock.lock();
        try {
            return readResponseCore(frameEndByte, timeoutMs, 1024 * 1024);
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public byte[] readExact(int length) {
        transactionLock.lock();
        try {
            return readExactCore(length);
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public byte[] readUntil(byte endByte, int maxLength) {
        transactionLock.lock();
        try {
            return readResponseCore(endByte & 0xFF, readTimeout, maxLength);
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public byte[] sendAndReadExact(byte[] data, int length) {
        transactionLock.lock();
        try {
            return sendCore(data) ? readExactCore(length) : null;
        } finally {
            transactionLock.unlock();
        }
    }

    @Override
    public byte[] sendAndReadUntil(byte[] data, byte endByte, int maxLength) {
        transactionLock.lock();
        try {
            return sendCore(data)
                    ? readResponseCore(endByte & 0xFF, readTimeout, maxLength)
                    : null;
        } finally {
            transactionLock.unlock();
        }
    }

    @Override public byte[] readUntil(byte[] delimiter, int maxLength) {
        transactionLock.lock(); try { return readUntilCore(delimiter, maxLength); }
        finally { transactionLock.unlock(); }
    }
    @Override public byte[] sendAndReadUntil(byte[] data, byte[] delimiter, int maxLength) {
        transactionLock.lock(); try { return sendCore(data) ? readUntilCore(delimiter, maxLength) : null; }
        finally { transactionLock.unlock(); }
    }

    private byte[] readExactCore(int length) {
        if (length <= 0) {
            throw new IllegalArgumentException("Frame length must be greater than zero");
        }
        if (!isConnected() || inputStream == null) {
            logger.error("Serial port not connected, cannot read frame");
            return null;
        }
        byte[] result = new byte[length];
        int offset = 0;
        try {
            while (offset < length) {
                int count = inputStream.read(result, offset, length - offset);
                if (count < 0) {
                    return null;
                }
                offset += count;
            }
            return result;
        } catch (IOException e) {
            logger.error("Serial exact frame read error", e);
            return null;
        }
    }

    private byte[] readResponseCore(int frameEndByte, int timeoutMs, int maxLength) {
        if (timeoutMs <= 0 || maxLength <= 0) {
            throw new IllegalArgumentException(
                    "Frame timeout and maximum length must be greater than zero");
        }
        if (!isConnected() || inputStream == null) {
            logger.error("Serial port not connected, cannot read response");
            return null;
        }
        ByteArrayOutputStream baos = new ByteArrayOutputStream();
        long deadline = System.currentTimeMillis() + timeoutMs;
        try {
            while (System.currentTimeMillis() < deadline && baos.size() < maxLength) {
                int available = inputStream.available();
                if (available > 0) {
                    int b = inputStream.read();
                    baos.write(b);
                    if (b == (frameEndByte & 0xFF)) {
                        byte[] result = baos.toByteArray();
                        logger.debug("Serial readResponse complete: {} bytes", result.length);
                        return result;
                    }
                } else {
                    Thread.sleep(50);
                }
            }
            logger.error("Serial frame incomplete after {}ms or {} bytes; received {} bytes",
                    timeoutMs, maxLength, baos.size());
            return null;
        } catch (Exception e) {
            logger.error("Serial readResponse error", e);
            return null;
        }
    }

    /**
     * 清空串口输入缓冲区（防止残留数据导致响应错乱）
     */
    public void clearBuffer() {
        transactionLock.lock();
        try {
            clearBufferCore();
        } finally {
            transactionLock.unlock();
        }
    }

    private byte[] readUntilCore(byte[] delimiter, int maxLength) {
        if (delimiter == null || delimiter.length == 0 || maxLength < delimiter.length)
            throw new IllegalArgumentException("Invalid frame delimiter or maximum length");
        ByteArrayOutputStream result = new ByteArrayOutputStream();
        long deadline = System.currentTimeMillis() + readTimeout;
        try {
            while (System.currentTimeMillis() < deadline && result.size() < maxLength) {
                if (inputStream.available() == 0) { Thread.sleep(20); continue; }
                result.write(inputStream.read()); byte[] bytes = result.toByteArray();
                boolean match = bytes.length >= delimiter.length;
                for (int i = 0; match && i < delimiter.length; i++)
                    match = bytes[bytes.length - delimiter.length + i] == delimiter[i];
                if (match) return bytes;
            }
            return null;
        } catch (Exception e) { logger.error("Serial delimited frame read error", e); return null; }
    }

    private void clearBufferCore() {
        if (!isConnected() || inputStream == null) {
            return;
        }
        try {
            int drained = 0;
            while (inputStream.available() > 0) {
                inputStream.read();
                drained++;
            }
            if (drained > 0) {
                logger.debug("Serial cleared {} bytes from input buffer", drained);
            }
        } catch (IOException e) {
            logger.warn("Serial clearBuffer error", e);
        }
    }

    private void disconnectCore() {
        try {
            if (inputStream != null) inputStream.close();
        } catch (IOException e) {
            logger.debug("Serial input close error", e);
        } finally {
            inputStream = null;
        }
        try {
            if (outputStream != null) outputStream.close();
        } catch (IOException e) {
            logger.debug("Serial output close error", e);
        } finally {
            outputStream = null;
        }
        if (serialPort != null) {
            serialPort.closePort();
            serialPort = null;
        }
        connected = false;
    }

    @Override
    public void close() {
        disconnect();
        executor.shutdown();
    }
}

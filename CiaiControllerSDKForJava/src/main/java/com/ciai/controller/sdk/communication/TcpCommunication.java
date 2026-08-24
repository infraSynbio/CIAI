package com.ciai.controller.sdk.communication;

import com.ciai.controller.sdk.interface_.ICommunication;
import com.ciai.controller.sdk.interface_.IFramedCommunication;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.locks.ReentrantLock;

/**
 * TCP通信实现
 */
public class TcpCommunication implements IFramedCommunication, AutoCloseable {

    private static final Logger logger = LoggerFactory.getLogger(TcpCommunication.class);

    private final String host;
    private final int port;
    private final int connectTimeout;
    private final int readTimeout;
    private final int writeTimeout;
    private final ReentrantLock transactionLock = new ReentrantLock(true);
    private Socket socket;
    private InputStream inputStream;
    private OutputStream outputStream;
    private final ExecutorService executor = Executors.newFixedThreadPool(2);

    public TcpCommunication(String host, int port) {
        this(host, port, 5000);
    }

    public TcpCommunication(String host, int port, int timeout) {
        this(host, port, timeout, timeout, timeout);
    }

    public TcpCommunication(String host, int port, int connectTimeout, int readTimeout, int writeTimeout) {
        if (host == null || host.trim().isEmpty()) {
            throw new IllegalArgumentException("TCP host is required");
        }
        if (port <= 0 || port > 65535) {
            throw new IllegalArgumentException("TCP port must be between 1 and 65535");
        }
        if (connectTimeout <= 0 || readTimeout <= 0 || writeTimeout <= 0) {
            throw new IllegalArgumentException("TCP timeouts must be greater than zero");
        }
        this.host = host;
        this.port = port;
        this.connectTimeout = connectTimeout;
        this.readTimeout = readTimeout;
        this.writeTimeout = writeTimeout;
    }

    @Override
    public boolean isConnected() {
        return socket != null && socket.isConnected() && !socket.isClosed();
    }

    @Override
    public CompletableFuture<Boolean> connectAsync() {
        return CompletableFuture.supplyAsync(() -> connect(), executor);
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
        try {
            disconnectCore();
            socket = new Socket();
            socket.connect(new InetSocketAddress(host, port), connectTimeout);
            socket.setSoTimeout(readTimeout);
            inputStream = socket.getInputStream();
            outputStream = socket.getOutputStream();
            logger.info("TCP connected to {}:{}", host, port);
            return true;
        } catch (IOException e) {
            disconnectCore();
            logger.error("TCP connection failed: {}:{}", host, port, e);
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
            logger.info("TCP disconnected from {}:{}", host, port);
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
            logger.error("TCP not connected, cannot send");
            return false;
        }
        try {
            outputStream.write(data);
            outputStream.flush();
            logger.debug("TCP sent {} bytes", data.length);
            return true;
        } catch (IOException e) {
            logger.error("TCP send error", e);
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
            logger.error("TCP not connected, cannot receive");
            return null;
        }
        try {
            byte[] buffer = new byte[4096];
            int bytesRead = inputStream.read(buffer);
            if (bytesRead > 0) {
                byte[] result = new byte[bytesRead];
                System.arraycopy(buffer, 0, result, 0, bytesRead);
                logger.debug("TCP received {} bytes", bytesRead);
                return result;
            }
            return null;
        } catch (IOException e) {
            logger.error("TCP receive error", e);
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
            return readUntilCore(endByte, maxLength);
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
            return sendCore(data) ? readUntilCore(endByte, maxLength) : null;
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
            logger.error("TCP not connected, cannot read frame");
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
            logger.error("TCP exact frame read error", e);
            return null;
        }
    }

    private byte[] readUntilCore(byte endByte, int maxLength) {
        if (maxLength <= 0) {
            throw new IllegalArgumentException("Maximum frame length must be greater than zero");
        }
        if (!isConnected() || inputStream == null) {
            logger.error("TCP not connected, cannot read frame");
            return null;
        }
        ByteArrayOutputStream result = new ByteArrayOutputStream();
        try {
            while (result.size() < maxLength) {
                int value = inputStream.read();
                if (value < 0) {
                    return null;
                }
                result.write(value);
                if ((byte) value == endByte) {
                    return result.toByteArray();
                }
            }
            logger.error("TCP frame exceeded maximum length: {}", maxLength);
            return null;
        } catch (IOException e) {
            logger.error("TCP delimited frame read error", e);
            return null;
        }
    }

    private byte[] readUntilCore(byte[] delimiter, int maxLength) {
        if (delimiter == null || delimiter.length == 0 || maxLength < delimiter.length)
            throw new IllegalArgumentException("Invalid frame delimiter or maximum length");
        ByteArrayOutputStream result = new ByteArrayOutputStream();
        try {
            while (result.size() < maxLength) {
                int value = inputStream.read(); if (value < 0) return null; result.write(value);
                byte[] bytes = result.toByteArray(); boolean match = bytes.length >= delimiter.length;
                for (int i = 0; match && i < delimiter.length; i++)
                    match = bytes[bytes.length - delimiter.length + i] == delimiter[i];
                if (match) return bytes;
            }
            return null;
        } catch (IOException e) { logger.error("TCP delimited frame read error", e); return null; }
    }

    private void disconnectCore() {
        try {
            if (inputStream != null) inputStream.close();
        } catch (IOException e) {
            logger.debug("TCP input close error", e);
        } finally {
            inputStream = null;
        }
        try {
            if (outputStream != null) outputStream.close();
        } catch (IOException e) {
            logger.debug("TCP output close error", e);
        } finally {
            outputStream = null;
        }
        try {
            if (socket != null) socket.close();
        } catch (IOException e) {
            logger.debug("TCP socket close error", e);
        } finally {
            socket = null;
        }
    }

    @Override
    public void close() {
        disconnect();
        executor.shutdown();
    }
}

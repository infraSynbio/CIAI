package com.ciai.controller.sdk.interface_;

import java.util.concurrent.CompletableFuture;

/**
 * 有明确帧边界的单通道通信。一次发送和对应读取始终占用同一个传输事务锁。
 */
public interface IFramedCommunication extends ICommunication {

    byte[] readExact(int length);

    byte[] readUntil(byte endByte, int maxLength);

    byte[] sendAndReadExact(byte[] data, int length);

    byte[] sendAndReadUntil(byte[] data, byte endByte, int maxLength);

    CompletableFuture<byte[]> readExactAsync(int length);

    CompletableFuture<byte[]> readUntilAsync(byte endByte, int maxLength);

    CompletableFuture<byte[]> sendAndReadExactAsync(byte[] data, int length);

    CompletableFuture<byte[]> sendAndReadUntilAsync(byte[] data, byte endByte, int maxLength);

    byte[] readUntil(byte[] delimiter, int maxLength);
    byte[] sendAndReadUntil(byte[] data, byte[] delimiter, int maxLength);
    CompletableFuture<byte[]> readUntilAsync(byte[] delimiter, int maxLength);
    CompletableFuture<byte[]> sendAndReadUntilAsync(byte[] data, byte[] delimiter, int maxLength);
}

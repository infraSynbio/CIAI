package com.ciai.controller.sdk.interface_;

import java.util.concurrent.CompletableFuture;

/**
 * 通信接口
 */
public interface ICommunication {

    /**
     * 是否已连接
     */
    boolean isConnected();

    /**
     * 异步连接
     */
    CompletableFuture<Boolean> connectAsync();

    /**
     * 异步断开连接
     */
    CompletableFuture<Void> disconnectAsync();

    /**
     * 异步发送数据
     */
    CompletableFuture<Boolean> sendAsync(byte[] data);

    /**
     * 异步接收数据
     */
    CompletableFuture<byte[]> receiveAsync();

    /**
     * 异步发送并接收数据
     */
    CompletableFuture<byte[]> sendAndReceiveAsync(byte[] data);

    /**
     * 同步连接
     */
    boolean connect();

    /**
     * 同步断开连接
     */
    void disconnect();

    /**
     * 同步发送数据
     */
    boolean send(byte[] data);

    /**
     * 同步接收数据
     */
    byte[] receive();

    /**
     * 同步发送并接收数据
     */
    byte[] sendAndReceive(byte[] data);
}

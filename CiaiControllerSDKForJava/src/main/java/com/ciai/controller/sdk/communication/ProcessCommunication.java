package com.ciai.controller.sdk.communication;

import com.ciai.controller.sdk.core.ConnectionConfiguration;
import com.ciai.controller.sdk.interface_.ICommunication;
import java.io.*;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.locks.ReentrantLock;

/** 旧版DLL/COM进程隔离连接：小端Int32长度前缀 + 原始请求/响应。 */
public class ProcessCommunication implements ICommunication, AutoCloseable {
    private static final int MAX_FRAME = 64 * 1024 * 1024;
    private final ConnectionConfiguration configuration;
    private final ReentrantLock lock = new ReentrantLock(true);
    private final ExecutorService executor = Executors.newCachedThreadPool();
    private Process process;
    private InputStream input;
    private OutputStream output;

    public ProcessCommunication(ConnectionConfiguration configuration) { this.configuration = configuration; }
    public boolean isConnected(){return process!=null&&process.isAlive()&&input!=null&&output!=null;}
    public CompletableFuture<Boolean> connectAsync(){return CompletableFuture.supplyAsync(this::connect,executor);}
    public CompletableFuture<Void> disconnectAsync(){return CompletableFuture.runAsync(this::disconnect,executor);}
    public CompletableFuture<Boolean> sendAsync(byte[] d){return CompletableFuture.supplyAsync(()->send(d),executor);}
    public CompletableFuture<byte[]> receiveAsync(){return CompletableFuture.supplyAsync(this::receive,executor);}
    public CompletableFuture<byte[]> sendAndReceiveAsync(byte[] d){return CompletableFuture.supplyAsync(()->sendAndReceive(d),executor);}

    public boolean connect(){
        lock.lock(); try {
            if(isConnected())return true; disconnectCore();
            java.util.List<String> command=new java.util.ArrayList<>(); command.add(configuration.getExecutable()); command.addAll(configuration.getArguments());
            ProcessBuilder builder=new ProcessBuilder(command);
            if(configuration.getWorkingDirectory()!=null&&!configuration.getWorkingDirectory().trim().isEmpty()) builder.directory(new File(configuration.getWorkingDirectory()));
            for(Map.Entry<String,String> e:configuration.getEnvironment().entrySet())builder.environment().put(e.getKey(),e.getValue());
            process=builder.start(); input=process.getInputStream(); output=process.getOutputStream(); drain(process.getErrorStream()); return true;
        }catch(Exception e){disconnectCore();return false;}finally{lock.unlock();}
    }
    public void disconnect(){lock.lock();try{disconnectCore();}finally{lock.unlock();}}
    public boolean send(byte[] d){lock.lock();try{writeFrame(d);return true;}catch(Exception e){return false;}finally{lock.unlock();}}
    public byte[] receive(){lock.lock();try{return readFrame();}catch(Exception e){return null;}finally{lock.unlock();}}
    public byte[] sendAndReceive(byte[] d){lock.lock();try{writeFrame(d);return readFrame();}catch(Exception e){return null;}finally{lock.unlock();}}
    private void writeFrame(byte[] data)throws IOException{if(!isConnected())throw new EOFException();if(data==null)data=new byte[0];if(data.length>MAX_FRAME)throw new IOException("Frame too large");writeIntLE(output,data.length);output.write(data);output.flush();}
    private byte[] readFrame()throws IOException{int n=readIntLE(input);if(n<0||n>MAX_FRAME)throw new IOException("Invalid frame length: "+n);byte[] b=new byte[n];int o=0;while(o<n){int r=input.read(b,o,n-o);if(r<0)throw new EOFException();o+=r;}return b;}
    private static void writeIntLE(OutputStream o,int v)throws IOException{o.write(v);o.write(v>>>8);o.write(v>>>16);o.write(v>>>24);}
    private static int readIntLE(InputStream i)throws IOException{int a=i.read(),b=i.read(),c=i.read(),d=i.read();if((a|b|c|d)<0)throw new EOFException();return a|(b<<8)|(c<<16)|(d<<24);}
    private void drain(final InputStream error){executor.submit(()->{try{byte[] b=new byte[1024];while(error.read(b)>=0){}}catch(IOException ignored){}});}
    private void disconnectCore(){try{if(output!=null)output.close();}catch(Exception ignored){}try{if(input!=null)input.close();}catch(Exception ignored){}output=null;input=null;if(process!=null){process.destroy();try{if(!process.waitFor(configuration.getShutdownTimeoutMs(),java.util.concurrent.TimeUnit.MILLISECONDS))process.destroyForcibly();}catch(Exception ignored){}process=null;}}
    public void close(){disconnect();executor.shutdown();}
}

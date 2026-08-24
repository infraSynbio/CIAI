package com.ciai.controller.sdk.service;

import java.io.IOException;
import java.nio.file.*;
import java.util.UUID;
import java.util.concurrent.CancellationException;
import java.util.function.BooleanSupplier;

/** 文件型设备的路径隔离、稳定检测和原子写入。 */
public class FileWorkflow {
    private final Path rootDirectory;
    public FileWorkflow(String rootDirectory) throws IOException {
        if(rootDirectory==null||rootDirectory.trim().isEmpty())throw new IllegalArgumentException("File root is required");
        this.rootDirectory=Paths.get(rootDirectory).toAbsolutePath().normalize();Files.createDirectories(this.rootDirectory);
    }
    public Path resolve(String relativePath){
        if(relativePath==null||relativePath.trim().isEmpty())throw new IllegalArgumentException("Relative path is required");
        Path result=rootDirectory.resolve(relativePath).normalize();
        if(!result.startsWith(rootDirectory))throw new IllegalArgumentException("Path escapes the allowed file root");
        return result;
    }
    public Path waitForStableFile(String relativePath,long timeoutMs,long stableMs,BooleanSupplier cancelled)throws Exception{
        Path path=resolve(relativePath);long deadline=System.currentTimeMillis()+timeoutMs;long previous=-1,unchanged=System.currentTimeMillis();
        while(System.currentTimeMillis()<deadline){
            if(cancelled!=null&&cancelled.getAsBoolean())throw new CancellationException("File wait cancelled");
            if(Files.exists(path)){long size=Files.size(path);if(size==previous&&System.currentTimeMillis()-unchanged>=stableMs)return path;if(size!=previous){previous=size;unchanged=System.currentTimeMillis();}}
            Thread.sleep(100);
        }
        throw new java.util.concurrent.TimeoutException("Stable file timeout: "+relativePath);
    }
    public void writeAtomic(String relativePath,byte[] data)throws IOException{
        Path target=resolve(relativePath);if(target.getParent()!=null)Files.createDirectories(target.getParent());
        Path temporary=target.resolveSibling(target.getFileName()+".tmp-"+UUID.randomUUID().toString().replace("-",""));
        try{Files.write(temporary,data==null?new byte[0]:data,StandardOpenOption.CREATE_NEW);try{Files.move(temporary,target,StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING);}catch(AtomicMoveNotSupportedException e){Files.move(temporary,target,StandardCopyOption.REPLACE_EXISTING);}}
        finally{Files.deleteIfExists(temporary);}
    }
}

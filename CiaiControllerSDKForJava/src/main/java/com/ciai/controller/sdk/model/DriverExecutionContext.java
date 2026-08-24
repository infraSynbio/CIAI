package com.ciai.controller.sdk.model;

import java.util.concurrent.CancellationException;
import java.util.concurrent.atomic.AtomicBoolean;

/** 长任务执行上下文。驱动可轮询或抛出取消异常。 */
public class DriverExecutionContext {
    private final String instructionId;
    private final String nestId;
    private final AtomicBoolean cancelled = new AtomicBoolean();
    public DriverExecutionContext(String instructionId,String nestId){this.instructionId=instructionId;this.nestId=nestId;}
    public String getInstructionId(){return instructionId;}
    public String getNestId(){return nestId;}
    public boolean isCancellationRequested(){return cancelled.get();}
    public void throwIfCancellationRequested(){if(cancelled.get())throw new CancellationException("Instruction cancelled");}
    public void cancel(){cancelled.set(true);}
}

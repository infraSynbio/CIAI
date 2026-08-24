package com.ciai.controller.sdk.model;

import java.time.Instant;

/** 进度、报警和设备主动上报事件。 */
public class DriverEvent {
    private String type;
    private String instructionId;
    private String nestId;
    private Double progress;
    private String message;
    private Object data;
    private Instant timestamp = Instant.now();
    public String getType(){return type;} public void setType(String v){type=v;}
    public String getInstructionId(){return instructionId;} public void setInstructionId(String v){instructionId=v;}
    public String getNestId(){return nestId;} public void setNestId(String v){nestId=v;}
    public Double getProgress(){return progress;} public void setProgress(Double v){progress=v;}
    public String getMessage(){return message;} public void setMessage(String v){message=v;}
    public Object getData(){return data;} public void setData(Object v){data=v;}
    public Instant getTimestamp(){return timestamp;} public void setTimestamp(Instant v){timestamp=v;}
}

package com.ciai.controller.sdk.model;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;

import java.util.List;

/**
 * 完成回调数据
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public class Finish {

    @JsonProperty("completion")
    private String completion;

    @JsonProperty("errorMsg")
    private String errorMsg;

    @JsonProperty("instructionId")
    private String instructionId;

    @JsonProperty("nestId")
    private String nestId;

    @JsonProperty("resultOutput")
    private List<ResultOutput> resultOutput;

    public Finish() {
    }

    public Finish(String completion) {
        this.completion = completion;
    }

    public Finish(String completion, String errorMsg) {
        this.completion = completion;
        this.errorMsg = errorMsg;
    }

    // Static factory methods
    public static Finish success() {
        return new Finish("finish");
    }

    public static Finish error(String errorMsg) {
        return new Finish("error", errorMsg);
    }

    // Getters and Setters
    public String getCompletion() {
        return completion;
    }

    public void setCompletion(String completion) {
        this.completion = completion;
    }

    public String getErrorMsg() {
        return errorMsg;
    }

    public void setErrorMsg(String errorMsg) {
        this.errorMsg = errorMsg;
    }

    public String getInstructionId() {
        return instructionId;
    }

    public void setInstructionId(String instructionId) {
        this.instructionId = instructionId;
    }

    public String getNestId() {
        return nestId;
    }

    public void setNestId(String nestId) {
        this.nestId = nestId;
    }

    public List<ResultOutput> getResultOutput() {
        return resultOutput;
    }

    public void setResultOutput(List<ResultOutput> resultOutput) {
        this.resultOutput = resultOutput;
    }

    /**
     * 结果输出
     */
    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class ResultOutput {
        @JsonProperty("name")
        private String name;



        @JsonProperty("resultData")
        private Object resultData;

        public ResultOutput() {
        }

        public ResultOutput(String key, Object resultData) {
            this.name = key;
            this.resultData = resultData;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public Object getResultData() {
            return resultData;
        }

        public void setResultData(Object resultData) {
            this.resultData = resultData;
        }

        /** Legacy alias for early CIAI2 drivers. Prefer {@link #getName()}. */
        @Deprecated
        public String getKey() {
            return name;
        }

        /** Legacy alias for early CIAI2 drivers. Prefer {@link #setName(String)}. */
        @Deprecated
        public void setKey(String key) {
            this.name = key;
        }

        /** Legacy alias for early CIAI2 drivers. Prefer {@link #getResultData()}. */
        @Deprecated
        public Object getValue() {
            return resultData;
        }

        /** Legacy alias for early CIAI2 drivers. Prefer {@link #setResultData(Object)}. */
        @Deprecated
        public void setValue(Object value) {
            this.resultData = value;
        }
    }
}

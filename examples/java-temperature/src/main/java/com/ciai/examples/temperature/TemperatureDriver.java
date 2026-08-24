package com.ciai.examples.temperature;

import com.ciai.controller.sdk.annotation.DeviceDriver;
import com.ciai.controller.sdk.annotation.DeviceEnterExit;
import com.ciai.controller.sdk.annotation.DeviceFunction;
import com.ciai.controller.sdk.annotation.DeviceGet;
import com.ciai.controller.sdk.annotation.DeviceNest;
import com.ciai.controller.sdk.annotation.DeviceOperation;
import com.ciai.controller.sdk.annotation.DeviceSet;
import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.model.EnterOrExitData;
import com.ciai.controller.sdk.model.EquipmentNest;
import com.ciai.controller.sdk.model.Finish;
import com.ciai.controller.sdk.model.FunctionData;
import com.ciai.controller.sdk.model.OperationData;
import com.ciai.controller.sdk.model.Result;

import java.util.Collections;

@DeviceDriver(
        name = "示例温控设备",
        nameEN = "Example Temperature Controller",
        model = "SIM-TC-1",
        manufacturer = "CIAI Community",
        version = "1.0.0",
        author = "infraSynbio contributors",
        equipmentClass = "TemperatureController",
        equipmentType = 1,
        functionalResources = 1,
        parallelizability = 0,
        canEmergencyStop = true)
public final class TemperatureDriver extends DeviceDriverBase {
    private double currentTemperature = 25;
    private double targetTemperature = 25;

    @DeviceFunction(
            name = "run_temperature",
            titleCN = "运行温控",
            titleEN = "Run temperature",
            description = "模拟运行到指定温度",
            defaultPeriod = 5,
            formJson = "{\"fields\":[{\"name\":\"targetCelsius\",\"type\":\"number\",\"label\":\"目标温度(°C)\"}]}")
    public Result<Finish> runTemperature(FunctionData data) throws InterruptedException {
        TemperatureParameter parameter = requireFunctionParam(data, TemperatureParameter.class);
        if (parameter.targetCelsius < 4 || parameter.targetCelsius > 95) {
            return Result.failed("目标温度必须在 4–95 °C 之间");
        }
        targetTemperature = parameter.targetCelsius;

        for (int progress = 0; progress <= 100; progress += 20) {
            getCurrentExecution().throwIfCancellationRequested();
            currentTemperature += (targetTemperature - currentTemperature) * 0.45;
            reportProgress(progress, "模拟温控运行中", null);
            Thread.sleep(100);
        }

        currentTemperature = targetTemperature;
        Finish finish = Finish.success();
        finish.setInstructionId(data.getInstructionId());
        finish.setNestId(data.getNestId());
        finish.setResultOutput(Collections.singletonList(
                new Finish.ResultOutput("temperatureCelsius", currentTemperature)));
        return Result.success(finish);
    }

    @DeviceOperation(name = "reset", titleCN = "复位", titleEN = "Reset")
    public Result<Boolean> reset(OperationData data) {
        currentTemperature = 25;
        targetTemperature = 25;
        return Result.success(true);
    }

    @DeviceSet(name = "target_temperature", titleCN = "目标温度", titleEN = "Target temperature", type = "number", unit = "°C")
    public Result<Boolean> setTargetTemperature(Double value) {
        if (value == null || value < 4 || value > 95) {
            return Result.failed("目标温度必须在 4–95 °C 之间");
        }
        targetTemperature = value;
        return Result.success(true);
    }

    @DeviceGet(name = "current_temperature", titleCN = "当前温度", titleEN = "Current temperature", type = "number", unit = "°C")
    public double getCurrentTemperature() {
        return currentTemperature;
    }

    @DeviceEnterExit(name = "transfer", titleCN = "样品转移", titleEN = "Sample transfer")
    public Result<Finish> transfer(EnterOrExitData data) {
        return Result.success(Finish.success());
    }

    @DeviceNest(order = 0)
    public EquipmentNest robotExchange() {
        EquipmentNest nest = new EquipmentNest();
        nest.setNestName("robot_exchange");
        nest.setNestDescription("外部机械臂交互位");
        nest.setNestAccessibility(1);
        nest.setNestIsDestination(1);
        nest.setLabwareType("MP96");
        return nest;
    }

    public static final class TemperatureParameter {
        public double targetCelsius;
    }
}

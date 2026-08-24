using System.ComponentModel.DataAnnotations;
using CiaiControllerSDK.Attributes;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Models;

[DeviceDriver(
    name: "示例温控设备",
    NameEN = "Example Temperature Controller",
    Model = "SIM-TC-1",
    Manufacturer = "CIAI Community",
    Version = "1.0.0",
    Author = "infraSynbio contributors",
    EquipmentClass = "TemperatureController",
    EquipmentType = 1,
    FunctionalResources = 1,
    Parallelizability = 0,
    CanEmergencyStop = true)]
public sealed class TemperatureDriver : DeviceDriverBase
{
    private double _currentTemperature = 25;
    private double _targetTemperature = 25;

    [DeviceFunction(
        "run_temperature",
        TitleCN = "运行温控",
        TitleEN = "Run temperature",
        Description = "模拟运行到指定温度",
        DefaultPeriod = 5,
        FormJson = "{\"fields\":[{\"name\":\"targetCelsius\",\"type\":\"number\",\"label\":\"目标温度(°C)\"}]}"
    )]
    public async Task<Result<Finish>> RunTemperatureAsync(FunctionData data)
    {
        var parameter = RequireFunctionParam<TemperatureParameter>(data);
        _targetTemperature = parameter.TargetCelsius;

        for (var progress = 0; progress <= 100; progress += 20)
        {
            ExecutionCancellationToken.ThrowIfCancellationRequested();
            _currentTemperature += (_targetTemperature - _currentTemperature) * 0.45;
            ReportProgress(progress, "模拟温控运行中");
            await Task.Delay(100, ExecutionCancellationToken);
        }

        _currentTemperature = _targetTemperature;
        return Result<Finish>.Success(new Finish
        {
            Completion = "finish",
            InstructionId = data.InstructionId,
            NestId = data.NestId,
            ResultOutput = new List<ResultOutput>
            {
                new() { Name = "temperatureCelsius", ResultData = _currentTemperature }
            }
        });
    }

    [DeviceOperation("reset", TitleCN = "复位", TitleEN = "Reset")]
    public Result<bool> Reset(OperationData data)
    {
        _currentTemperature = 25;
        _targetTemperature = 25;
        return Result<bool>.Success(true);
    }

    [DeviceSet("target_temperature", TitleCN = "目标温度", TitleEN = "Target temperature", Type = "number", Unit = "°C")]
    public Result<bool> SetTargetTemperature(double value)
    {
        if (value is < 4 or > 95)
            return Result<bool>.Failed("目标温度必须在 4–95 °C 之间");
        _targetTemperature = value;
        return Result<bool>.Success(true);
    }

    [DeviceGet("current_temperature", TitleCN = "当前温度", TitleEN = "Current temperature", Type = "number", Unit = "°C")]
    public double GetCurrentTemperature() => _currentTemperature;

    [DeviceEnterExit("transfer", TitleCN = "样品转移", TitleEN = "Sample transfer")]
    public Result<Finish> Transfer(EnterOrExitData data) =>
        Result<Finish>.Success(Finish.Success());

    [DeviceNest(Order = 0)]
    public EquipmentNest RobotExchange => new()
    {
        NestName = "robot_exchange",
        NestDescription = "外部机械臂交互位",
        NestAccessibility = 1,
        NestIsDestination = 1,
        LabwareType = "MP96"
    };
}

public sealed class TemperatureParameter
{
    [Range(4, 95)]
    public double TargetCelsius { get; set; }
}

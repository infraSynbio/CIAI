using CiaiControllerSDK.WebServer;

var configPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(AppContext.BaseDirectory, "application.yml");

await DriverHost.RunAsync<TemperatureDriver>(configPath);

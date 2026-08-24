# Legacy DLL adapter

Create an executable with the vendor-required target (`net472`), platform (`x86`/`x64`) and, for COM, `[STAThread]`. Reference this project and keep vendor DLLs beside that executable.

```csharp
[STAThread]
static void Main()
{
    var vendor = new VendorSdk();
    LegacyAdapterServer.Run(request => vendor.Execute(request));
}
```

Configure either the C# or Java SDK host with `type: process` and `executable`. The wire format is little-endian `Int32` length followed by raw bytes. The adapter must write logs to stderr because stdout is reserved for protocol frames.

[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.BetterstackSyslog.svg)](https://www.nuget.org/packages/Serilog.Sinks.BetterstackSyslog)

A [Serilog](https://serilog.net) sink that logs events to Better Stack.

### Getting started

Install the [Serilog.Sinks.BetterstackSyslog](https://www.nuget.org/packages/Serilog.Sinks.BetterstackSyslog) package from NuGet:

Using Package Manager
In Visual Studio, open **NuGet Package Manager Console** by clicking **Tools** → **NuGet Package Manager** → **Package Manager Console**

In the console run the following command:
```powershell
Install-Package Serilog.Sinks.BetterstackSyslog
```

Optionally using Dotnet CLI
```powershell
dotnet add package Serilog.Sinks.BetterstackSyslog
```

To start logging you need to setup a rsyslog source in Logtail, please use their documentation for this.
[BetterStack - Getting started](https://betterstack.com/docs/logs/logging-start/)
The new ingestion hosts required that they are suffixed with `-vec`, which might not always show in the source tab. It should look like: 
`sXXXXXX.eu-fsn-3-vec.betterstackdata.com`

This package support both Udp and Tcp thanks to the use of a well maintained package for syslog. Thanks to [Serilog.Sinks.SyslogMessages](https://github.com/IonxSolutions/serilog-sinks-syslog)

To configure the sink to write messages to logtail, call `WriteTo.BetterstackXXX()` with your Better Stack token during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.BetterstackXXX(host: "<YOUR_INGESTION_HOST>", port: 6514/6517, token: "$SOURCE_TOKEN")
    .CreateLogger();
```

For UDP:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.BetterstackUdp(host: "<YOUR_INGESTION_HOST>", port: 6517, token: "$SOURCE_TOKEN")
    .CreateLogger();
```

For TCP:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.BetterstackTcp(host: "<YOUR_INGESTION_HOST>", port: 6514, token: "$SOURCE_TOKEN")
    .CreateLogger();
```

A number of optional parameters are available for more advanced configurations, see doc tags for more information.


NOTE: Project is in no way associated with Better Stack or Serilog.
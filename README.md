[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.BetterstackSyslog.svg)](https://www.nuget.org/packages/Serilog.Sinks.BetterstackSyslog)

A [Serilog](https://serilog.net) sink that logs events to [Better Stack](https://betterstack.com/logs) over syslog (UDP or TCP/TLS).

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

To start logging you need to set up an rsyslog source in Better Stack, please use their documentation for this:
[Better Stack - Getting started](https://betterstack.com/docs/logs/logging-start/)

The new ingestion hosts require a `-vec` suffix, which might not always show in the source tab. It should look like:
`sXXXXXX.eu-fsn-3-vec.betterstackdata.com`

This package supports both UDP and TCP thanks to the well maintained [Serilog.Sinks.SyslogMessages](https://github.com/IonxSolutions/serilog-sinks-syslog).

To configure the sink, call `WriteTo.BetterstackUdp()` or `WriteTo.BetterstackTcp()` with your Better Stack source token during logger configuration.

For UDP:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.BetterstackUdp(host: "<YOUR_INGESTION_HOST>", port: 6517, token: "<SOURCE_TOKEN>")
    .CreateLogger();
```

For TCP (TLS by default):

```csharp
var log = new LoggerConfiguration()
    .WriteTo.BetterstackTcp(host: "<YOUR_INGESTION_HOST>", port: 6514, token: "<SOURCE_TOKEN>")
    .CreateLogger();
```

### DNS refresh (UDP)

Better Stack's ingestion hosts rotate between IPs, and UDP gives no error when sending to one that is gone, so logs would be silently lost.
The UDP sink therefore re-resolves the host after `dnsTtl` (default 1 minute) and only switches IP when the current one is no longer returned.

```csharp
.WriteTo.BetterstackUdp(host: "<YOUR_INGESTION_HOST>", port: 6517, token: "<SOURCE_TOKEN>", dnsTtl: TimeSpan.FromMinutes(5))
```

Use `Timeout.InfiniteTimeSpan` to resolve only once at startup. The TCP sink resolves the host whenever it reconnects, so it needs no setting.

A number of optional parameters are available for more advanced configurations, see doc tags for more information.


NOTE: Project is in no way associated with Better Stack or Serilog.

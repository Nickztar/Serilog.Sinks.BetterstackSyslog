// See https://aka.ms/new-console-template for more information

using Serilog;
using Serilog.Configuration;
using Serilog.Debugging;
using Serilog.Sinks.PeriodicBatching;

var logConfig = new LoggerConfiguration()
    .WriteTo.Console();
const string outputTemplate = "TCP: {Message}";
var log = logConfig
    .MinimumLevel.Debug()
    .WriteTo.BetterstackTcp(
        host: "<INGESTION_HOST>",
        port: 6514,
        token: "<SOURCE_TOKEN>",
        appName: "ExampleLogtailProject",
        outputTemplate: outputTemplate,
        batchConfig: new PeriodicBatchingSinkOptions()
        {
            EagerlyEmitFirstEvent = true
        }
    )
    .Enrich.FromLogContext()
    .CreateLogger();
    
SelfLog.Enable(Console.Error);

var name = "Syslog";
log.Information("Hello World! {Name}", name);
//Not required.
await log.DisposeAsync();

_ = name;

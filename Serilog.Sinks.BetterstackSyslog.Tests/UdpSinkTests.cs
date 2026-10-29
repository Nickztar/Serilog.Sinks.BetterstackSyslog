using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.BetterStackSyslog.Tests;

public class UdpSinkTests
{
    [Fact]
    public async Task KeepsSendingWhenDnsTtlExpires()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        // A zero TTL re-resolves before every batch.
        await using (var log = new LoggerConfiguration()
            .WriteTo.BetterstackUdp("127.0.0.1", port, "token", dnsTtl: TimeSpan.Zero,
                batchConfig: new PeriodicBatchingSinkOptions { BatchSizeLimit = 1, Period = TimeSpan.FromMilliseconds(10) })
            .CreateLogger())
        {
            log.Information("first");
            await Task.Delay(100);
            log.Information("second");
        }

        var first = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var second = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Encoding.UTF8.GetString(first.Buffer).Should().Contain("first");
        Encoding.UTF8.GetString(second.Buffer).Should().Contain("second");
    }
}

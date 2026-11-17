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

    private static readonly IPAddress V4 = IPAddress.Parse("195.63.225.127");
    private static readonly IPAddress OtherV4 = IPAddress.Parse("195.63.225.128");
    private static readonly IPAddress V6 = IPAddress.Parse("2a0f:1b80:0:51::d6");

    [Fact]
    public void PrefersIpv4()
    {
        ResolvingSyslogUdpSink.Pick([V6, V4], null, keepCurrent: true).Should().Be(V4);
        ResolvingSyslogUdpSink.Pick([V6], null, keepCurrent: true).Should().Be(V6);
    }

    [Fact]
    public void MovesOffIpv6WhenIpv4Available()
    {
        ResolvingSyslogUdpSink.Pick([V6, V4], V6, keepCurrent: true).Should().Be(V4);
    }

    [Fact]
    public void StaysOnCurrentIpv4UnlessSendFailed()
    {
        ResolvingSyslogUdpSink.Pick([OtherV4, V4], V4, keepCurrent: true).Should().Be(V4);
        ResolvingSyslogUdpSink.Pick([V4, OtherV4], V4, keepCurrent: false).Should().Be(OtherV4);
        ResolvingSyslogUdpSink.Pick([V4], V4, keepCurrent: false).Should().Be(V4);
    }
}

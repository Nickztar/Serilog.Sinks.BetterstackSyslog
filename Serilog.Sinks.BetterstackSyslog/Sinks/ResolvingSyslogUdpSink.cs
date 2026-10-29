using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Serilog.Sinks.Syslog;

namespace Serilog.Sinks.BetterStackSyslog;

/// <summary>
/// Wraps <see cref="SyslogUdpSink"/> and re-resolves the host once <c>dnsTtl</c> has passed,
/// since the ingestion hosts rotate IPs and UDP gives no error when sending to a dead one.
/// </summary>
internal sealed class ResolvingSyslogUdpSink : IBatchedLogEventSink, IDisposable
{
    private readonly string host;
    private readonly int port;
    private readonly ISyslogFormatter formatter;
    private readonly TimeSpan dnsTtl;
    private IPEndPoint endpoint;
    private SyslogUdpSink sink;
    private DateTime resolvedAt;

    public ResolvingSyslogUdpSink(string host, int port, ISyslogFormatter formatter, TimeSpan dnsTtl)
    {
        this.host = host;
        this.port = port;
        this.formatter = formatter;
        this.dnsTtl = dnsTtl;
        endpoint = new IPEndPoint(Lookup(host)[0], port);
        sink = new SyslogUdpSink(endpoint, formatter);
        resolvedAt = DateTime.UtcNow;
    }

    public Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        // PeriodicBatchingSink never calls this concurrently, so no locking needed.
        if (dnsTtl != System.Threading.Timeout.InfiniteTimeSpan && DateTime.UtcNow - resolvedAt >= dnsTtl)
            Refresh();
        return sink.EmitBatchAsync(batch);
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    public void Dispose() => sink.Dispose();

    private static IPAddress[] Lookup(string host)
    {
        var addresses = Dns.GetHostAddresses(host)
            .Where(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .ToArray();

        return addresses.Length > 0 ? addresses : throw new SocketException((int)SocketError.HostNotFound);
    }

    private void Refresh()
    {
        resolvedAt = DateTime.UtcNow;
        IPAddress[] addresses;
        try
        {
            addresses = Lookup(host);
        }
        catch (Exception ex)
        {
            // Keep sending to the old address, try again after the next TTL.
            SelfLog.WriteLine($"[{nameof(ResolvingSyslogUdpSink)}] failed to resolve {host}, keeping {endpoint.Address} - {ex.Message}");
            return;
        }

        // Pools return the same IPs in rotating order, only move when ours is gone.
        if (addresses.Contains(endpoint.Address))
            return;

        var old = sink;
        endpoint = new IPEndPoint(addresses[0], port);
        sink = new SyslogUdpSink(endpoint, formatter);
        old.Dispose();
    }
}

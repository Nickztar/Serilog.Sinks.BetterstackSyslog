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
internal sealed class ResolvingSyslogUdpSink(string host, int port, ISyslogFormatter formatter, TimeSpan dnsTtl)
    : IBatchedLogEventSink, IDisposable
{
    // Resolved on the first batch, so building the logger never blocks on DNS.
    private IPEndPoint? endpoint;
    private SyslogUdpSink? sink;
    private DateTime resolvedAt;

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        // PeriodicBatchingSink never calls this concurrently, so no locking needed.
        if (sink is null)
        {
            // Throwing here lets PeriodicBatchingSink keep the batch and retry with backoff.
            endpoint = new IPEndPoint((await Lookup(host))[0], port);
            sink = new SyslogUdpSink(endpoint, formatter);
            resolvedAt = DateTime.UtcNow;
        }
        else if (dnsTtl != System.Threading.Timeout.InfiniteTimeSpan && DateTime.UtcNow - resolvedAt >= dnsTtl)
        {
            await Refresh();
        }
        await sink.EmitBatchAsync(batch);
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    public void Dispose() => sink?.Dispose();

    private static async Task<IPAddress[]> Lookup(string host)
    {
        var possible = await Dns.GetHostAddressesAsync(host);
        var addresses = possible
            .Where(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .ToArray();

        return addresses.Length > 0 ? addresses : throw new SocketException((int)SocketError.HostNotFound);
    }

    private async Task Refresh()
    {
        var current = endpoint!.Address;
        resolvedAt = DateTime.UtcNow;
        IPAddress[] addresses;
        try
        {
            addresses = await Lookup(host);
        }
        catch (Exception ex)
        {
            // Keep sending to the old address, try again after the next TTL.
            SelfLog.WriteLine($"[{nameof(ResolvingSyslogUdpSink)}] failed to resolve {host}, keeping {current} - {ex.Message}");
            return;
        }

        // Pools return the same IPs in rotating order, only move when ours is gone.
        if (addresses.Contains(current))
            return;

        var old = sink;
        endpoint = new IPEndPoint(addresses[0], port);
        sink = new SyslogUdpSink(endpoint, formatter);
        old?.Dispose();
    }
}

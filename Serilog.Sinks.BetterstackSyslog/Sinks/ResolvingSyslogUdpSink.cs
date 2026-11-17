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
    private bool sendFailed;

    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        // PeriodicBatchingSink never calls this concurrently, so no locking needed.
        if (sink is null)
        {
            // Throwing here lets PeriodicBatchingSink keep the batch and retry with backoff.
            endpoint = new IPEndPoint(Pick(await Lookup(host), null, keepCurrent: false), port);
            sink = new SyslogUdpSink(endpoint, formatter);
            resolvedAt = DateTime.UtcNow;
        }
        else if (dnsTtl != System.Threading.Timeout.InfiniteTimeSpan && DateTime.UtcNow - resolvedAt >= dnsTtl)
        {
            await Refresh();
        }
        try
        {
            await sink.EmitBatchAsync(batch);
        }
        catch (SocketException)
        {
            // Re-resolve before the retry and move off this address instead of retrying it forever.
            sendFailed = true;
            resolvedAt = DateTime.MinValue;
            throw;
        }
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

    /// <summary>
    /// Picks IPv4 when the host has any (IPv6 only as fallback), since hosts like Azure Container Apps
    /// have no IPv6 egress and UDP to an unreachable address fails silently.
    /// </summary>
    internal static IPAddress Pick(IPAddress[] addresses, IPAddress? current, bool keepCurrent)
    {
        var preferred = addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray();
        if (preferred.Length == 0)
            preferred = addresses;

        // Pools return the same IPs in rotating order, so stay on ours while it is still a preferred pick
        // rather than flapping every TTL. After a send failure, move to another one if there is any.
        if (current is not null && preferred.Contains(current))
            return keepCurrent ? current : preferred.FirstOrDefault(x => !x.Equals(current)) ?? current;

        return preferred[0];
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

        var next = Pick(addresses, current, keepCurrent: !sendFailed);
        sendFailed = false;
        if (next.Equals(current))
            return;

        var old = sink;
        endpoint = new IPEndPoint(next, port);
        sink = new SyslogUdpSink(endpoint, formatter);
        old?.Dispose();
    }
}

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Identity.Api.Diagnostics;

public sealed record MachinePingResult(DateTimeOffset CheckedAt, string Status, string? Address = null, long? RoundTripMilliseconds = null);
public interface IMachinePingService
{
    Task<MachinePingResult> Check(string hostname, CancellationToken ct);
}
public interface IMachinePingTransport
{
    Task<MachinePingResult> Check(string hostname, CancellationToken ct);
}

// Only called with the hostname read from an active enrollment, never a request-supplied target.
public sealed partial class MachinePingService(IMachinePingTransport transport) : IMachinePingService, IDisposable
{
    private readonly SemaphoreSlim slots = new(4);
    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsHostname();

    public async Task<MachinePingResult> Check(string hostname, CancellationToken ct)
    {
        if (!WindowsHostname().IsMatch(hostname) || IPAddress.TryParse(hostname, out _) || hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return new(DateTimeOffset.UtcNow, "unsupportedHostname");
        if (!await slots.WaitAsync(0, ct)) return new(DateTimeOffset.UtcNow, "busy");
        try { return await transport.Check(hostname, ct); }
        finally { slots.Release(); }
    }
    public void Dispose() => slots.Dispose();
}

public sealed class SystemMachinePingTransport : IMachinePingTransport
{
    public async Task<MachinePingResult> Check(string hostname, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        IPAddress? address = null;
        try
        {
            using var dnsDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            dnsDeadline.CancelAfter(TimeSpan.FromSeconds(2));
            var addresses = await Dns.GetHostAddressesAsync(hostname, dnsDeadline.Token);
            address = addresses.Select(a => a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a)
                .Where(IsUnicastTarget).OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault();
            if (address is null) return new(DateTimeOffset.UtcNow, "unresolved");
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(2), new byte[32], new PingOptions(64, false), deadline.Token);
            return reply.Status == IPStatus.Success
                ? new(DateTimeOffset.UtcNow, "reachable", address.ToString(), reply.RoundtripTime)
                : new(DateTimeOffset.UtcNow, "noReply", address.ToString());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new(DateTimeOffset.UtcNow, address is null ? "unresolved" : "noReply", address?.ToString()); }
        catch (SocketException) { return new(DateTimeOffset.UtcNow, "unresolved"); }
        catch (Exception e) when (e is PingException or UnauthorizedAccessException or PlatformNotSupportedException)
        { return new(DateTimeOffset.UtcNow, "unavailable", address?.ToString()); }
    }

    public static bool IsUnicastTarget(IPAddress address) => !IPAddress.IsLoopback(address)
        && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any)
        && !address.IsIPv6Multicast
        && (address.AddressFamily != AddressFamily.InterNetwork || address.GetAddressBytes()[0] is > 0 and < 224);
}

using System.Collections.Concurrent;
using System.Net;

namespace Directo.Signaling.Server;

/// <summary>Caps concurrent connections per remote address and globally.</summary>
public sealed class AddressLimiter(SignalingOptions options)
{
    private readonly ConcurrentDictionary<IPAddress, int> _perAddress = new();
    private int _total;

    public bool TryAcquire(IPAddress? address)
    {
        if (Interlocked.Increment(ref _total) > options.MaxConnections)
        {
            Interlocked.Decrement(ref _total);
            return false;
        }

        var key = Normalize(address);
        var count = _perAddress.AddOrUpdate(key, 1, static (_, c) => c + 1);
        if (count > options.MaxConnectionsPerAddress)
        {
            Release(address);
            return false;
        }

        return true;
    }

    public void Release(IPAddress? address)
    {
        Interlocked.Decrement(ref _total);
        var key = Normalize(address);
        while (_perAddress.TryGetValue(key, out var count))
        {
            if (count <= 1 ? _perAddress.TryRemove(new KeyValuePair<IPAddress, int>(key, count)) : _perAddress.TryUpdate(key, count - 1, count))
            {
                return;
            }
        }
    }

    private static IPAddress Normalize(IPAddress? address)
    {
        if (address is null)
        {
            return IPAddress.None;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        // Treat an IPv6 /64 as one subscriber.
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes);
        }

        return address;
    }
}

using System.Collections.Concurrent;
using Murmur.Domain.Ports;

namespace Murmur.IntegrationTests.Support;

/// <summary>Stands in for Android Keystore-backed storage. Shared across restarts of the same test device.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, byte[]> _secrets = new();

    public Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.TryGetValue(name, out var value) ? (byte[])value.Clone() : null);

    public Task SetAsync(string name, byte[] value, CancellationToken cancellationToken = default)
    {
        _secrets[name] = (byte[])value.Clone();
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        _secrets.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}

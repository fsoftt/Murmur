using Murmur.Client;
using Murmur.Domain.Ports;
using Murmur.Networking.Links;
using Microsoft.Extensions.Logging;

namespace Murmur.App.Services;

/// <summary>Owns the single <see cref="MurmurClient"/> of the app process.</summary>
public sealed class ClientHost(ISecretStore secrets, AppSettings settings, ILoggerFactory loggerFactory)
{
    private MurmurClient? _client;

    public MurmurClient Client => _client ?? throw new InvalidOperationException("Client not initialized yet.");

    public async Task InitializeAsync()
    {
        if (_client is not null)
        {
            return;
        }

        var client = await MurmurClient.OpenAsync(
            new MurmurClientOptions
            {
                DatabasePath = Path.Combine(FileSystem.AppDataDirectory, "murmur.db"),
                SignalingEndpoint = settings.SignalingEndpoint,
            },
            secrets,
            CreateLinkFactory(),
            loggerFactory: loggerFactory);
        await client.StartAsync();
        _client = client;
    }

    private static IPeerLinkFactory CreateLinkFactory() =>
#if MURMUR_DEV_RELAY
        new DevRelayPeerLinkFactory();
#else
        new UnavailablePeerLinkFactory();
#endif
}

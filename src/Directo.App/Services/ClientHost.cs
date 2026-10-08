using Directo.Client;
using Directo.Domain.Ports;
using Directo.Networking.Links;
using Microsoft.Extensions.Logging;

namespace Directo.App.Services;

/// <summary>Owns the single <see cref="DirectoClient"/> of the app process.</summary>
public sealed class ClientHost(ISecretStore secrets, AppSettings settings, ILoggerFactory loggerFactory)
{
    private DirectoClient? _client;

    public DirectoClient Client => _client ?? throw new InvalidOperationException("Client not initialized yet.");

    public async Task InitializeAsync()
    {
        if (_client is not null)
        {
            return;
        }

        var client = await DirectoClient.OpenAsync(
            new DirectoClientOptions
            {
                DatabasePath = Path.Combine(FileSystem.AppDataDirectory, "directo.db"),
                SignalingEndpoint = settings.SignalingEndpoint,
            },
            secrets,
            CreateLinkFactory(),
            loggerFactory: loggerFactory);
        await client.StartAsync();
        _client = client;
    }

    private static IPeerLinkFactory CreateLinkFactory() =>
#if DIRECTO_DEV_RELAY
        new DevRelayPeerLinkFactory();
#else
        new UnavailablePeerLinkFactory();
#endif
}

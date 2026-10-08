using Microsoft.Extensions.Logging;
using Murmur.Client;
using Murmur.Domain.Ports;
using Murmur.Networking.Links;

namespace Murmur.App.Services;

/// <summary>
/// Owns the single <see cref="MurmurClient"/> of the app process. The UI, the background
/// delivery job and the "always available" service may all start it, possibly at the same time.
/// </summary>
public sealed class ClientHost(ISecretStore secrets, AppSettings settings, ILoggerFactory loggerFactory, IIncomingMessageNotifier notifier)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MurmurClient? _client;

    public MurmurClient Client => _client ?? throw new InvalidOperationException("Client not initialized yet.");

    public bool IsInitialized => _client is not null;

    public async Task InitializeAsync()
    {
        if (_client is not null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
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
            notifier.Attach(client);
            _client = client;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IPeerLinkFactory CreateLinkFactory() =>
#if MURMUR_DEV_RELAY
        new DevRelayPeerLinkFactory();
#else
        new UnavailablePeerLinkFactory();
#endif
}

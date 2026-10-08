using Directo.Client;
using Directo.Domain.Common;
using Directo.Domain.Delivery;
using Directo.Domain.Model;
using Directo.Networking.Links;
using Directo.Networking.Sessions;

namespace Directo.IntegrationTests.Support;

/// <summary>A complete Directo device (identity, SQLCipher database, signaling, sessions) that can be stopped and restarted.</summary>
public sealed class TestDevice : IAsyncDisposable
{
    private static readonly DeliveryOptions FastDelivery = new() { Retransmission = new Backoff(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)) };

    private static readonly ConnectionOptions FastConnections = new()
    {
        EstablishTimeout = TimeSpan.FromSeconds(5),
        Reconnect = new Backoff(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500)),
    };

    private readonly SignalingServerFixture _server;
    private readonly InMemoryPeerLinkNetwork _network;
    private readonly InMemorySecretStore _secrets = new();
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"directo-it-{Guid.NewGuid():N}.db");
    private readonly string _profileName;

    private TestDevice(string profileName, SignalingServerFixture server, InMemoryPeerLinkNetwork network)
    {
        _profileName = profileName;
        _server = server;
        _network = network;
    }

    public DirectoClient Client { get; private set; } = null!;

    public bool IsRunning { get; private set; }

    public static async Task<TestDevice> StartAsync(string profileName, SignalingServerFixture server, InMemoryPeerLinkNetwork network)
    {
        var device = new TestDevice(profileName, server, network);
        await device.StartAsync();
        await device.Client.SetProfileNameAsync(profileName);
        return device;
    }

    public async Task StartAsync()
    {
        Client = await DirectoClient.OpenAsync(
            new DirectoClientOptions
            {
                DatabasePath = _databasePath,
                SignalingEndpoint = _server.Endpoint,
                SignalingConnector = _server.Connector,
                Delivery = FastDelivery,
                Connections = FastConnections,
            },
            _secrets,
            _network);
        await Client.StartAsync();
        IsRunning = true;
    }

    /// <summary>Simulates the app being killed: everything in memory is gone, the disk stays.</summary>
    public async Task StopAsync()
    {
        if (IsRunning)
        {
            IsRunning = false;
            await Client.DisposeAsync();
        }
    }

    public async Task<Contact> SingleContactAsync() => Assert.Single(await Client.Contacts.ListAsync());

    public async Task<IReadOnlyList<Message>> HistoryAsync()
    {
        var contact = await SingleContactAsync();
        var conversation = await Client.GetConversationAsync(contact.Id);
        return await Client.LoadMessagesAsync(conversation.Id);
    }

    public async Task<Message?> FindAsync(MessageId id) => (await HistoryAsync()).SingleOrDefault(m => m.Id == id);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        foreach (var file in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            File.Delete(file);
        }
    }
}

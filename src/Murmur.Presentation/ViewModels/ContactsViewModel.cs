using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;
using Murmur.Domain.Common;
using Murmur.Domain.Model;
using Murmur.Networking.Signaling;
using Murmur.Presentation.Formatting;
using Murmur.Presentation.Services;

namespace Murmur.Presentation.ViewModels;

public sealed record ConversationListItem(
    ContactId ContactId,
    string DisplayName,
    string? Preview,
    string ConnectionStatus,
    int PendingCount,
    string Initials,
    string AvatarColor,
    PresenceKind Presence,
    bool IsVerified,
    string ActivityTime)
{
    public bool HasPending => PendingCount > 0;

    public bool IsOnline => Presence == PresenceKind.Online;

    public bool IsConnecting => Presence == PresenceKind.Connecting;
}

/// <summary>Home screen: conversations with their state, plus entry points to pair new contacts.</summary>
public sealed partial class ContactsViewModel : ViewModelBase, IDisposable
{
    private readonly MurmurClient _client;
    private readonly IUiDispatcher _dispatcher;
    private readonly INavigator _navigator;
    private int _reloadQueued;

    public ContactsViewModel(MurmurClient client, IUiDispatcher dispatcher, INavigator navigator)
    {
        _client = client;
        _dispatcher = dispatcher;
        _navigator = navigator;
        _client.Events.ContactsChanged += OnChanged;
        _client.Events.MessageStored += OnChanged;
        _client.Events.MessageStatusChanged += OnChanged;
        _client.Events.ConnectionStateChanged += OnChanged;
        _client.SignalingStateChanged += OnSignalingChanged;
        Banner = UserMessages.ForSignaling(_client.SignalingState);
    }

    public ObservableCollection<ConversationListItem> Conversations { get; } = [];

    /// <summary>Shown when the signaling service is unreachable.</summary>
    [ObservableProperty]
    public partial string? Banner { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        var summaries = await _client.ListConversationsAsync();
        var contacts = (await _client.Contacts.ListAsync()).ToDictionary(c => c.Id);
        var now = DateTimeOffset.Now;
        Conversations.Clear();
        foreach (var summary in summaries)
        {
            if (!contacts.TryGetValue(summary.ContactId, out var contact))
            {
                continue;
            }

            var state = _client.GetConnectionState(summary.ContactId);
            Conversations.Add(new ConversationListItem(
                summary.ContactId,
                summary.DisplayName,
                summary.LastMessagePreview ?? (contact.Verification == VerificationState.Verified ? "Sin mensajes todavía" : "Emparejados · compara el código de seguridad"),
                contact.IsBlocked ? "Bloqueado" : UserMessages.ForConnection(state),
                summary.PendingCount,
                Avatars.Initials(summary.DisplayName),
                Avatars.ColorFor(contact.Identity.IdentityKey),
                UserMessages.ForPresence(state),
                contact.Verification == VerificationState.Verified,
                UserMessages.ForActivity(summary.LastActivityAt, now)));
        }

        IsEmpty = Conversations.Count == 0;
    });

    [RelayCommand]
    private Task OpenAsync(ConversationListItem item) => _navigator.OpenChatAsync(item.ContactId);

    [RelayCommand]
    private Task InviteAsync() => _navigator.OpenInviteAsync();

    [RelayCommand]
    private Task ScanAsync() => _navigator.OpenScannerAsync();

    public void Dispose()
    {
        _client.Events.ContactsChanged -= OnChanged;
        _client.Events.MessageStored -= OnChanged;
        _client.Events.MessageStatusChanged -= OnChanged;
        _client.Events.ConnectionStateChanged -= OnChanged;
        _client.SignalingStateChanged -= OnSignalingChanged;
    }

    private void OnChanged(object? sender, object e)
    {
        // Coalesce bursts (e.g. 100 ACKs) into one reload.
        if (Interlocked.Exchange(ref _reloadQueued, 1) == 0)
        {
            _dispatcher.Post(async () =>
            {
                Interlocked.Exchange(ref _reloadQueued, 0);
                await LoadAsync();
            });
        }
    }

    private void OnChanged(object? sender, EventArgs e) => OnChanged(sender, (object)e);

    private void OnSignalingChanged(object? sender, SignalingState state) =>
        _dispatcher.Post(() => Banner = UserMessages.ForSignaling(state));
}

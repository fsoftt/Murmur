using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Directo.Client;
using Directo.Domain.Common;
using Directo.Domain.Model;
using Directo.Networking.Signaling;
using Directo.Presentation.Formatting;
using Directo.Presentation.Services;

namespace Directo.Presentation.ViewModels;

public sealed record ConversationListItem(ContactId ContactId, string DisplayName, string? Preview, string ConnectionStatus, int PendingCount)
{
    public bool HasPending => PendingCount > 0;
}

/// <summary>Home screen: conversations with their state, plus entry points to pair new contacts.</summary>
public sealed partial class ContactsViewModel : ViewModelBase, IDisposable
{
    private readonly DirectoClient _client;
    private readonly IUiDispatcher _dispatcher;
    private readonly INavigator _navigator;
    private int _reloadQueued;

    public ContactsViewModel(DirectoClient client, IUiDispatcher dispatcher, INavigator navigator)
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
        Conversations.Clear();
        foreach (var summary in summaries)
        {
            Conversations.Add(new ConversationListItem(
                summary.ContactId,
                summary.DisplayName,
                summary.LastMessagePreview,
                UserMessages.ForConnection(_client.GetConnectionState(summary.ContactId)),
                summary.PendingCount));
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

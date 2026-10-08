using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;
using Murmur.Domain.Common;
using Murmur.Domain.Model;
using Murmur.Presentation.Formatting;
using Murmur.Presentation.Services;

namespace Murmur.Presentation.ViewModels;

/// <summary>One conversation: history, composing, live delivery states and connection status.</summary>
public sealed partial class ChatViewModel(MurmurClient client, IUiDispatcher dispatcher, INavigator navigator) : ViewModelBase, IDisposable
{
    private ContactId _contactId;
    private ConversationId _conversationId;
    private bool _subscribed;

    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVerified { get; set; }

    [ObservableProperty]
    public partial string Initials { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AvatarColor { get; set; } = "#2563EB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnline), nameof(IsConnecting))]
    public partial PresenceKind Presence { get; set; }

    public bool IsOnline => Presence == PresenceKind.Online;

    public bool IsConnecting => Presence == PresenceKind.Connecting;

    /// <summary>Messages in one run must be this close in time to share a bubble group.</summary>
    public static readonly TimeSpan GroupWindow = TimeSpan.FromMinutes(5);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(HasDraft))]
    public partial string Draft { get; set; } = string.Empty;

    public bool HasDraft => CanSend();

    public async Task LoadAsync(ContactId contactId)
    {
        _contactId = contactId;
        await RunAsync(async () =>
        {
            var contact = await client.Contacts.GetAsync(contactId)
                ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
            var conversation = await client.GetConversationAsync(contactId);
            _conversationId = conversation.Id;
            Title = contact.DisplayName;
            IsVerified = contact.Verification == VerificationState.Verified;
            Initials = Avatars.Initials(contact.DisplayName);
            AvatarColor = Avatars.ColorFor(contact.Identity.IdentityKey);
            var state = client.GetConnectionState(contactId);
            ConnectionStatus = UserMessages.ForConnection(state);
            Presence = UserMessages.ForPresence(state);
            Subscribe();
            var history = await client.LoadMessagesAsync(conversation.Id);
            Messages.Clear();
            foreach (var message in history)
            {
                Messages.Add(new MessageItemViewModel(message));
            }

            Regroup();
        });
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync() => RunAsync(async () =>
    {
        var text = Draft;
        Draft = string.Empty;
        try
        {
            // The message appears through the MessageStored event, already persisted.
            await client.SendMessageAsync(_contactId, text);
        }
        catch (MurmurException)
        {
            Draft = text;
            throw;
        }
    });

    [RelayCommand]
    private Task RetryAsync(MessageItemViewModel message) => RunAsync(() => client.RetryMessageAsync(_conversationId, message.Id));

    [RelayCommand]
    private Task OpenDetailsAsync() => navigator.OpenContactDetailsAsync(_contactId);

    public void Dispose()
    {
        if (_subscribed)
        {
            client.Events.MessageStored -= OnMessageStored;
            client.Events.MessageStatusChanged -= OnStatusChanged;
            client.Events.ConnectionStateChanged -= OnConnectionChanged;
            _subscribed = false;
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(Draft);

    /// <summary>Recomputes bubble runs: same side, each message within <see cref="GroupWindow"/> of the previous one.</summary>
    private void Regroup()
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            var current = Messages[i];
            var previous = i > 0 ? Messages[i - 1] : null;
            var next = i < Messages.Count - 1 ? Messages[i + 1] : null;
            current.IsFirstInGroup = previous is null || !SameRun(previous, current);
            current.IsLastInGroup = next is null || !SameRun(current, next);
        }
    }

    private static bool SameRun(MessageItemViewModel earlier, MessageItemViewModel later) =>
        earlier.IsOutgoing == later.IsOutgoing && later.DisplayedAt - earlier.DisplayedAt < GroupWindow;

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        client.Events.MessageStored += OnMessageStored;
        client.Events.MessageStatusChanged += OnStatusChanged;
        client.Events.ConnectionStateChanged += OnConnectionChanged;
        _subscribed = true;
    }

    private void OnMessageStored(object? sender, Message message)
    {
        if (message.ConversationId != _conversationId)
        {
            return;
        }

        dispatcher.Post(() =>
        {
            if (Messages.Any(m => m.Id == message.Id))
            {
                return;
            }

            // Keep Lamport order: an incoming message may logically precede ones already shown.
            var item = new MessageItemViewModel(message);
            var index = Messages.Count;
            while (index > 0 && Messages[index - 1].SortKey.CompareTo(item.SortKey) > 0)
            {
                index--;
            }

            Messages.Insert(index, item);
            Regroup();
        });
    }

    private void OnStatusChanged(object? sender, MessageStatusChange change)
    {
        if (change.ConversationId != _conversationId)
        {
            return;
        }

        dispatcher.Post(() =>
        {
            var item = Messages.FirstOrDefault(m => m.Id == change.MessageId);
            if (item is not null)
            {
                item.Status = change.Status;
            }
        });
    }

    private void OnConnectionChanged(object? sender, ConnectionStateChange change)
    {
        if (change.ContactId == _contactId)
        {
            dispatcher.Post(() =>
            {
                ConnectionStatus = UserMessages.ForConnection(change.State);
                Presence = UserMessages.ForPresence(change.State);
            });
        }
    }
}

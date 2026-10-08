using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Directo.Client;
using Directo.Domain.Common;
using Directo.Domain.Model;
using Directo.Presentation.Formatting;
using Directo.Presentation.Services;

namespace Directo.Presentation.ViewModels;

/// <summary>One conversation: history, composing, live delivery states and connection status.</summary>
public sealed partial class ChatViewModel(DirectoClient client, IUiDispatcher dispatcher, INavigator navigator) : ViewModelBase, IDisposable
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
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = string.Empty;

    public async Task LoadAsync(ContactId contactId)
    {
        _contactId = contactId;
        await RunAsync(async () =>
        {
            var contact = await client.Contacts.GetAsync(contactId)
                ?? throw new DirectoException(DirectoErrorCode.ContactNotFound, "Contact not found.");
            var conversation = await client.GetConversationAsync(contactId);
            _conversationId = conversation.Id;
            Title = contact.DisplayName;
            IsVerified = contact.Verification == VerificationState.Verified;
            ConnectionStatus = UserMessages.ForConnection(client.GetConnectionState(contactId));
            Subscribe();
            var history = await client.LoadMessagesAsync(conversation.Id);
            Messages.Clear();
            foreach (var message in history)
            {
                Messages.Add(new MessageItemViewModel(message));
            }
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
        catch (DirectoException)
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
            dispatcher.Post(() => ConnectionStatus = UserMessages.ForConnection(change.State));
        }
    }
}

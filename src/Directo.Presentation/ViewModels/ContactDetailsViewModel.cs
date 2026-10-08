using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Directo.Client;
using Directo.Domain.Model;
using Directo.Presentation.Services;

namespace Directo.Presentation.ViewModels;

/// <summary>Contact management: rename, safety number verification, block, delete history.</summary>
public sealed partial class ContactDetailsViewModel(DirectoClient client, INavigator navigator) : ViewModelBase
{
    private ContactId _contactId;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    /// <summary>60 digits; both people must see exactly the same.</summary>
    [ObservableProperty]
    public partial string SafetyNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVerified { get; set; }

    [ObservableProperty]
    public partial bool IsBlocked { get; set; }

    public Task LoadAsync(ContactId contactId)
    {
        _contactId = contactId;
        return RunAsync(async () =>
        {
            var contact = await client.Contacts.GetAsync(contactId)
                ?? throw new DirectoException(DirectoErrorCode.ContactNotFound, "Contact not found.");
            DisplayName = contact.DisplayName;
            IsVerified = contact.Verification == VerificationState.Verified;
            IsBlocked = contact.IsBlocked;
            SafetyNumber = await client.GetSafetyNumberAsync(contactId);
        });
    }

    [RelayCommand]
    private Task SaveNameAsync() => RunAsync(() => client.Contacts.RenameAsync(_contactId, DisplayName));

    [RelayCommand]
    private Task SetVerifiedAsync(bool verified) => RunAsync(async () =>
    {
        await client.Contacts.MarkVerifiedAsync(_contactId, verified);
        IsVerified = verified;
    });

    [RelayCommand]
    private Task SetBlockedAsync(bool blocked) => RunAsync(async () =>
    {
        await client.Contacts.SetBlockedAsync(_contactId, blocked);
        IsBlocked = blocked;
    });

    /// <summary>Deletes local history only; the UI must say that the other person keeps their copy.</summary>
    [RelayCommand]
    private Task ClearHistoryAsync() => RunAsync(async () =>
    {
        var conversation = await client.GetConversationAsync(_contactId);
        await client.Contacts.ClearConversationAsync(conversation.Id);
    });

    [RelayCommand]
    private Task DeleteContactAsync() => RunAsync(async () =>
    {
        await client.Contacts.DeleteAsync(_contactId);
        await navigator.GoBackAsync();
    });
}

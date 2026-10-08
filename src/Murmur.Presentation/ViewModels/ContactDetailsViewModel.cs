using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;
using Murmur.Domain.Model;
using Murmur.Presentation.Formatting;
using Murmur.Presentation.Services;

namespace Murmur.Presentation.ViewModels;

/// <summary>Contact management: rename, safety number verification, block, delete history.</summary>
public sealed partial class ContactDetailsViewModel(MurmurClient client, INavigator navigator) : ViewModelBase
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

    [ObservableProperty]
    public partial string Initials { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AvatarColor { get; set; } = "#2563EB";

    /// <summary>The safety number as twelve 5-digit groups, for a grid layout.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> SafetyGroups { get; set; } = [];

    /// <summary>8×8 mirrored visual fingerprint (palette index or -1), identical on both phones.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<int> FingerprintCells { get; set; } = [];

    public Task LoadAsync(ContactId contactId)
    {
        _contactId = contactId;
        return RunAsync(async () =>
        {
            var contact = await client.Contacts.GetAsync(contactId)
                ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
            DisplayName = contact.DisplayName;
            IsVerified = contact.Verification == VerificationState.Verified;
            IsBlocked = contact.IsBlocked;
            Initials = Avatars.Initials(contact.DisplayName);
            AvatarColor = Avatars.ColorFor(contact.Identity.IdentityKey);
            SafetyNumber = await client.GetSafetyNumberAsync(contactId);
            SafetyGroups = Fingerprint.Groups(SafetyNumber);
            FingerprintCells = Fingerprint.Cells(SafetyNumber);
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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;
using Murmur.Domain.Model;
using Murmur.Presentation.Formatting;
using Murmur.Presentation.Services;

namespace Murmur.Presentation.ViewModels;

/// <summary>Shown right after pairing; nudges the user to compare the safety number.</summary>
public sealed partial class PairedViewModel(MurmurClient client, INavigator navigator) : ViewModelBase
{
    private ContactId _contactId;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Initials { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AvatarColor { get; set; } = "#7C3AED";

    [ObservableProperty]
    public partial string MyInitials { get; set; } = "TÚ";

    public Task LoadAsync(ContactId contactId)
    {
        _contactId = contactId;
        return RunAsync(async () =>
        {
            var contact = await client.Contacts.GetAsync(contactId)
                ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
            DisplayName = contact.DisplayName;
            Initials = Avatars.Initials(contact.DisplayName);
            AvatarColor = Avatars.ColorFor(contact.Identity.IdentityKey);
            var myName = await client.GetProfileNameAsync();
            MyInitials = string.IsNullOrWhiteSpace(myName) ? "TÚ" : Avatars.Initials(myName);
        });
    }

    [RelayCommand]
    private Task OpenChatAsync() => navigator.OpenChatAsync(_contactId);

    [RelayCommand]
    private Task CompareCodeAsync() => navigator.OpenContactDetailsAsync(_contactId);
}

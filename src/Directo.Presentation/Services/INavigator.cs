using Directo.Domain.Model;

namespace Directo.Presentation.Services;

/// <summary>Navigation as seen by view models; implemented with MAUI Shell routes.</summary>
public interface INavigator
{
    Task OpenChatAsync(ContactId contactId);

    Task OpenContactDetailsAsync(ContactId contactId);

    /// <summary>The celebration screen shown right after a successful pairing.</summary>
    Task OpenPairedAsync(ContactId contactId);

    Task OpenInviteAsync();

    Task OpenScannerAsync();

    Task GoBackAsync();
}

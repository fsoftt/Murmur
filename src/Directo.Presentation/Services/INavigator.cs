using Directo.Domain.Model;

namespace Directo.Presentation.Services;

/// <summary>Navigation as seen by view models; implemented with MAUI Shell routes.</summary>
public interface INavigator
{
    Task OpenChatAsync(ContactId contactId);

    Task OpenContactDetailsAsync(ContactId contactId);

    Task OpenInviteAsync();

    Task OpenScannerAsync();

    Task GoBackAsync();
}

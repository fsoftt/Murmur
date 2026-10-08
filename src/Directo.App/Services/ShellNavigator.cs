using Directo.Domain.Model;
using Directo.Presentation.Services;

namespace Directo.App.Services;

public sealed class ShellNavigator : INavigator
{
    public const string ChatRoute = "chat";
    public const string ContactRoute = "contact";
    public const string InviteRoute = "invite";
    public const string ScanRoute = "scan";
    public const string PairedRoute = "paired";
    public const string SettingsRoute = "settings";
    public const string ContactIdParameter = "contactId";

    public Task OpenChatAsync(ContactId contactId) => Go($"//contacts/{ChatRoute}?{ContactIdParameter}={contactId}");

    public Task OpenContactDetailsAsync(ContactId contactId) => Go($"{ContactRoute}?{ContactIdParameter}={contactId}");

    // Replaces the invite or scanner page, so "back" from the celebration returns home.
    public Task OpenPairedAsync(ContactId contactId) => Go($"//contacts/{PairedRoute}?{ContactIdParameter}={contactId}");

    public Task OpenInviteAsync() => Go(InviteRoute);

    public Task OpenScannerAsync() => Go(ScanRoute);

    public Task GoBackAsync() => Go("..");

    public static ContactId? ParseContactId(IDictionary<string, object> query) =>
        query.TryGetValue(ContactIdParameter, out var value) && Guid.TryParse(value?.ToString(), out var id) ? new ContactId(id) : null;

    private static Task Go(string route) => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route));
}

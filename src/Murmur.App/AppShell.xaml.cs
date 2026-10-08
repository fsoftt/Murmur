using Murmur.App.Services;
using Murmur.App.Views;

namespace Murmur.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(ShellNavigator.ChatRoute, typeof(ChatPage));
        Routing.RegisterRoute(ShellNavigator.ContactRoute, typeof(ContactDetailsPage));
        Routing.RegisterRoute(ShellNavigator.InviteRoute, typeof(InvitePage));
        Routing.RegisterRoute(ShellNavigator.ScanRoute, typeof(ScanPage));
        Routing.RegisterRoute(ShellNavigator.SettingsRoute, typeof(SettingsPage));
        Routing.RegisterRoute(ShellNavigator.PairedRoute, typeof(PairedPage));
    }
}

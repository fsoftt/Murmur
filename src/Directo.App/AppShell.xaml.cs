using Directo.App.Services;
using Directo.App.Views;

namespace Directo.App;

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
    }
}

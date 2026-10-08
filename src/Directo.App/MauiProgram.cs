using Directo.App.Services;
using Directo.App.Views;
using Directo.Client;
using Directo.Domain.Ports;
using Directo.Presentation.Services;
using Directo.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace Directo.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
#else
        // Production logs stay minimal (architecture §27).
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
#endif

        var services = builder.Services;
        services.AddSingleton<ISecretStore, MauiSecretStore>();
        services.AddSingleton<IUiDispatcher, MauiDispatcher>();
        services.AddSingleton<INavigator, ShellNavigator>();
        services.AddSingleton<AppSettings>();
        services.AddSingleton<ClientHost>();
        services.AddSingleton<DirectoClient>(sp => sp.GetRequiredService<ClientHost>().Client);

        services.AddTransient<AppShell>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<InviteViewModel>();
        services.AddTransient<ScanInviteViewModel>();
        services.AddTransient<ContactDetailsViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<ContactsPage>();
        services.AddTransient<ChatPage>();
        services.AddTransient<InvitePage>();
        services.AddTransient<ScanPage>();
        services.AddTransient<ContactDetailsPage>();
        services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}

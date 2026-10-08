using Murmur.App.Services;
using Murmur.App.Views;
using Murmur.Client;
using Murmur.Domain.Ports;
using Murmur.Presentation.Services;
using Murmur.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace Murmur.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                // Bundled OFL fonts (see Resources/Fonts/OFL-LICENSES.txt): no runtime download.
                fonts.AddFont("DMSans_400Regular.ttf", "DMSans");
                fonts.AddFont("DMSans_500Medium.ttf", "DMSansMedium");
                fonts.AddFont("DMSans_600SemiBold.ttf", "DMSansSemiBold");
                fonts.AddFont("SpaceGrotesk_600SemiBold.ttf", "SpaceGroteskSemiBold");
                fonts.AddFont("SpaceGrotesk_700Bold.ttf", "SpaceGroteskBold");
                fonts.AddFont("JetBrainsMono_500Medium.ttf", "JetBrainsMono");
            });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
#else
        // Production logs stay minimal (architecture §27).
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
#endif

        var services = builder.Services;
        services.AddSingleton<ISecretStore, MauiSecretStore>();
#if ANDROID
        services.AddSingleton<IAvailabilityService, Platforms.Android.Background.AndroidAvailabilityService>();
        services.AddSingleton<IIncomingMessageNotifier, Platforms.Android.Background.AndroidMessageNotifier>();
#else
        services.AddSingleton<IAvailabilityService, UnsupportedAvailabilityService>();
        services.AddSingleton<IIncomingMessageNotifier, NoIncomingMessageNotifier>();
#endif
        services.AddSingleton<IUiDispatcher, MauiDispatcher>();
        services.AddSingleton<INavigator, ShellNavigator>();
        services.AddSingleton<AppSettings>();
        services.AddSingleton<ClientHost>();
        services.AddSingleton<MurmurClient>(sp => sp.GetRequiredService<ClientHost>().Client);

        services.AddTransient<AppShell>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<InviteViewModel>();
        services.AddTransient<ScanInviteViewModel>();
        services.AddTransient<ContactDetailsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<PairedViewModel>();

        services.AddTransient<ContactsPage>();
        services.AddTransient<ChatPage>();
        services.AddTransient<InvitePage>();
        services.AddTransient<ScanPage>();
        services.AddTransient<ContactDetailsPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<PairedPage>();

        return builder.Build();
    }
}

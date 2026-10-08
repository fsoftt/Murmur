using Murmur.App.Services;
using Murmur.App.Views;
using Murmur.Presentation.Services;

namespace Murmur.App;

public partial class App : Application
{
    private readonly ClientHost _host;
    private readonly IServiceProvider _services;
    private readonly IAvailabilityService _availability;

    public App(ClientHost host, IServiceProvider services, IAvailabilityService availability)
    {
        InitializeComponent();
        _host = host;
        _services = services;
        _availability = availability;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new StartupPage("Abriendo tu almacenamiento cifrado…"));
        AppVisibility.Set(true);
        window.Resumed += (_, _) =>
        {
            AppVisibility.Set(true);
            _availability.ResumeIfEnabled();
        };
        window.Stopped += (_, _) =>
        {
            AppVisibility.Set(false);

            // Leaving the app: let the background job keep trying to deliver (option A).
            _availability.ScheduleBackgroundDelivery();
        };
        _ = OpenAsync(window);
        return window;
    }

    /// <summary>Restores local state before showing anything that depends on it (architecture §21).</summary>
    private async Task OpenAsync(Window window)
    {
        try
        {
            await _host.InitializeAsync();
            _availability.ScheduleBackgroundDelivery();
            _availability.ResumeIfEnabled();
            window.Page = await _host.Client.IsOnboardedAsync()
                ? _services.GetRequiredService<AppShell>()
                : _services.GetRequiredService<OnboardingPage>();
        }
        catch (Exception)
        {
            // Never show stack traces to the user (architecture §37).
            window.Page = new StartupPage("No se pudo abrir el almacenamiento local cifrado de Murmur en este dispositivo.");
        }
    }
}

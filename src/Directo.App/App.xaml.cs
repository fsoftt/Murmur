using Directo.App.Services;
using Directo.App.Views;

namespace Directo.App;

public partial class App : Application
{
    private readonly ClientHost _host;
    private readonly IServiceProvider _services;

    public App(ClientHost host, IServiceProvider services)
    {
        InitializeComponent();
        _host = host;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new StartupPage("Abriendo tu almacenamiento cifrado…"));
        _ = OpenAsync(window);
        return window;
    }

    /// <summary>Restores local state before showing anything that depends on it (architecture §21).</summary>
    private async Task OpenAsync(Window window)
    {
        try
        {
            await _host.InitializeAsync();
            window.Page = _services.GetRequiredService<AppShell>();
        }
        catch (Exception)
        {
            // Never show stack traces to the user (architecture §37).
            window.Page = new StartupPage("No se pudo abrir el almacenamiento local cifrado de Directo en este dispositivo.");
        }
    }
}

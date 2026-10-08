using Murmur.App.Services;
using Murmur.Presentation.ViewModels;

namespace Murmur.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly AppSettings _settings;

    public SettingsPage(SettingsViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _settings = settings;
        EndpointEntry.Text = settings.SignalingEndpoint.ToString();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnSaveEndpointClicked(object? sender, EventArgs e)
    {
        var text = EndpointEntry.Text?.Trim() ?? string.Empty;
        if (!AppSettings.IsValidEndpoint(text))
        {
            await DisplayAlertAsync("Dirección no válida", "Usa una dirección wss:// (cifrada).", "Aceptar");
            return;
        }

        _settings.SignalingEndpoint = new Uri(text);
        await DisplayAlertAsync("Guardado", "Reinicia la app para usar el nuevo servidor.", "Aceptar");
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}

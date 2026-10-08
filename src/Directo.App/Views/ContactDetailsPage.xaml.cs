using Directo.App.Services;
using Directo.Presentation.ViewModels;

namespace Directo.App.Views;

public partial class ContactDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ContactDetailsViewModel _viewModel;

    public ContactDetailsPage(ContactDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (ShellNavigator.ParseContactId(query) is { } contactId)
        {
            await _viewModel.LoadAsync(contactId);
        }
    }

    private async void OnVerifiedToggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value != _viewModel.IsVerified)
        {
            await _viewModel.SetVerifiedCommand.ExecuteAsync(e.Value);
        }
    }

    private async void OnBlockedToggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value != _viewModel.IsBlocked)
        {
            await _viewModel.SetBlockedCommand.ExecuteAsync(e.Value);
        }
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        // Be honest about what deletion can and cannot do (architecture §24).
        if (await DisplayAlertAsync(
            "Borrar historial",
            "Se borrarán los mensajes de este dispositivo, incluidos los pendientes de entregar. La otra persona conserva su copia.",
            "Borrar",
            "Cancelar"))
        {
            await _viewModel.ClearHistoryCommand.ExecuteAsync(null);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync(
            "Eliminar contacto",
            "Se eliminará el contacto y el historial de este dispositivo. Para volver a hablar tendréis que emparejaros de nuevo.",
            "Eliminar",
            "Cancelar"))
        {
            await _viewModel.DeleteContactCommand.ExecuteAsync(null);
        }
    }
}

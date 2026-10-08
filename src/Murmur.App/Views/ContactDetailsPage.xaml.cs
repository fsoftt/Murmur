using Murmur.App.Graphics;
using Murmur.App.Services;
using Murmur.Presentation.ViewModels;

namespace Murmur.App.Views;

public partial class ContactDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ContactDetailsViewModel _viewModel;

    public ContactDetailsPage(ContactDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContactDetailsViewModel.IsVerified))
            {
                ShowVerification();
            }
        };
        Application.Current!.RequestedThemeChanged += (_, _) => ShowVerification();
        ShowVerification();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (ShellNavigator.ParseContactId(query) is { } contactId)
        {
            await _viewModel.LoadAsync(contactId);
        }
    }

    private void ShowVerification()
    {
        var verified = _viewModel.IsVerified;
        BadgeText.Text = verified ? "Identidad verificada" : "Sin verificar";
        var ink = Theme.Get(verified ? "OnlineText" : "Amber");
        BadgeText.TextColor = ink;
        BadgeIcon.Stroke = ink;
        Badge.BackgroundColor = Theme.Get(verified ? "OnlineSoft" : "AmberSoft");
        VerifyButton.Text = verified ? "Marcar como no verificado" : "He comprobado que coincide";
        VerifyButton.Style = verified ? (Style)Application.Current!.Resources["SecondaryButton"] : null;
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        var verify = !_viewModel.IsVerified;
        await _viewModel.SetVerifiedCommand.ExecuteAsync(verify);
        if (verify)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
    }

    private async void OnBlockedToggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value != _viewModel.IsBlocked)
        {
            await _viewModel.SetBlockedCommand.ExecuteAsync(e.Value);
        }
    }

    private async void OnNameUnfocused(object? sender, FocusEventArgs e) => await _viewModel.SaveNameCommand.ExecuteAsync(null);

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        // Be honest about what deletion can and cannot do (architecture §24).
        if (await DisplayAlertAsync(
            "Borrar historial",
            "Se borrarán los mensajes de este teléfono, incluidos los pendientes de entregar. La otra persona conserva su copia.",
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
            "Se eliminará el contacto y el historial de este teléfono. Para volver a hablar tendréis que emparejaros de nuevo.",
            "Eliminar",
            "Cancelar"))
        {
            await _viewModel.DeleteContactCommand.ExecuteAsync(null);
        }
    }
}

using Directo.App.Services;
using Directo.Presentation.ViewModels;

namespace Directo.App.Views;

public partial class ChatPage : ContentPage, IQueryAttributable
{
    private readonly ChatViewModel _viewModel;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.IsVerified))
            {
                UnverifiedHint.IsVisible = !_viewModel.IsVerified;
            }
        };
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (ShellNavigator.ParseContactId(query) is { } contactId)
        {
            await _viewModel.LoadAsync(contactId);
            UnverifiedHint.IsVisible = !_viewModel.IsVerified;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!Navigation.NavigationStack.Contains(this))
        {
            _viewModel.Dispose();
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MessageItemViewModel message)
        {
            return;
        }

        MessagesView.SelectedItem = null;
        if (message.CanRetry && await DisplayAlertAsync("Mensaje no entregado", "¿Volver a intentarlo?", "Reintentar", "Cancelar"))
        {
            await _viewModel.RetryCommand.ExecuteAsync(message);
        }
        else if (message.IsOutgoing)
        {
            await DisplayAlertAsync("Estado", message.StatusDescription, "Aceptar");
        }
    }
}

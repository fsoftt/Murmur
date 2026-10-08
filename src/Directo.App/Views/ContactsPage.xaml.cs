using Directo.App.Services;
using Directo.Presentation.ViewModels;

namespace Directo.App.Views;

public partial class ContactsPage : ContentPage
{
    private readonly ContactsViewModel _viewModel;

    public ContactsPage(ContactsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ConversationListItem item)
        {
            List.SelectedItem = null;
            _viewModel.OpenCommand.Execute(item);
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(ShellNavigator.SettingsRoute);
}

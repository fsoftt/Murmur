using System.ComponentModel;
using Directo.App.Graphics;
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
        _viewModel.PropertyChanged += OnViewModelChanged;
        ShowBanner();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContactsViewModel.Banner))
        {
            ShowBanner();
        }
    }

    /// <summary>The status card turns amber with the banner text when the meeting service is unreachable.</summary>
    private void ShowBanner()
    {
        var offline = _viewModel.Banner is not null;
        SignalText.Text = _viewModel.Banner ?? "Visible para tus contactos · el servidor no guarda nada";
        SignalDot.Fill = offline ? Theme.Get("Amber") : Theme.Get("OnlineDot");
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ConversationListItem item)
        {
            List.SelectedItem = null;
            _viewModel.OpenCommand.Execute(item);
        }
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(ShellNavigator.SettingsRoute);
}

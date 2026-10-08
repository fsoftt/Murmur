using Directo.App.Services;
using Directo.Presentation.ViewModels;

namespace Directo.App.Views;

public partial class PairedPage : ContentPage, IQueryAttributable
{
    private readonly PairedViewModel _viewModel;
    private bool _visible;

    public PairedPage(PairedViewModel viewModel)
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);

        Avatars.Scale = 0.6;
        Avatars.Opacity = 0;
        await Task.WhenAll(Avatars.ScaleToAsync(1, 450, Easing.SpringOut), Avatars.FadeToAsync(1, 250));

        while (_visible)
        {
            Ring.Scale = 0.8;
            Ring.Opacity = 0.7;
            await Task.WhenAll(Ring.ScaleToAsync(1.6, 2000, Easing.CubicOut), Ring.FadeToAsync(0, 2000));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _visible = false;
    }
}

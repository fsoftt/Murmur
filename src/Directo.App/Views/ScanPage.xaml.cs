using Directo.Presentation.ViewModels;
using ZXing.Net.Maui;

namespace Directo.App.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanInviteViewModel _viewModel;
    private bool _animating;

    public ScanPage(ScanInviteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Camera.Options = new BarcodeReaderOptions { Formats = BarcodeFormat.QrCode, AutoRotate = true, Multiple = false };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        Camera.IsDetecting = status == PermissionStatus.Granted;
        _animating = true;
        _ = AnimateScanLineAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Camera.IsDetecting = false;
        _animating = false;
    }

    private async Task AnimateScanLineAsync()
    {
        while (_animating)
        {
            await ScanLine.TranslateToAsync(0, 232, 1300, Easing.CubicInOut);
            await ScanLine.TranslateToAsync(0, 0, 1300, Easing.CubicInOut);
        }
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results.FirstOrDefault()?.Value is { } value)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await _viewModel.AcceptAsync(value);
            });
        }
    }

    private async void OnPasteClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PastedInvite.Text))
        {
            await _viewModel.AcceptAsync(PastedInvite.Text);
        }
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}

using Directo.App.Services;
using Directo.Presentation.ViewModels;
using QRCoder;

namespace Directo.App.Views;

public partial class InvitePage : ContentPage
{
    private readonly InviteViewModel _viewModel;
    private IDispatcherTimer? _timer;

    public InvitePage(InviteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        CopyButton.IsVisible = AppSettings.IsDevelopmentBuild;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InviteViewModel.InviteText))
            {
                RenderQr(_viewModel.InviteText);
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.InviteText is null)
        {
            await _viewModel.CreateAsync();
        }

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            _viewModel.UpdateCountdown(DateTimeOffset.UtcNow);
            WaitingDot.Opacity = WaitingDot.Opacity > 0.5 ? 0.35 : 1;
        };
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        if (!Navigation.NavigationStack.Contains(this))
        {
            _viewModel.Dispose();
        }
    }

    private void RenderQr(string? text)
    {
        if (text is null)
        {
            QrImage.Source = null;
            return;
        }

        using var generator = new QRCodeGenerator();
        // Level Q (25% recovery) leaves room for the logo drawn over the center.
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10, [11, 15, 23], [255, 255, 255]);
        QrImage.Source = ImageSource.FromStream(() => new MemoryStream(png));
    }

    private async void OnRegenerateClicked(object? sender, EventArgs e) => await _viewModel.CreateAsync();

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_viewModel.InviteText is { } text)
        {
            await Clipboard.Default.SetTextAsync(text);
        }
    }
}

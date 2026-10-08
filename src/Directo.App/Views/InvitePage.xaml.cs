using Directo.App.Services;
using Directo.Presentation.ViewModels;
using QRCoder;

namespace Directo.App.Views;

public partial class InvitePage : ContentPage
{
    private readonly InviteViewModel _viewModel;

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
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
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
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        QrImage.Source = ImageSource.FromStream(() => new MemoryStream(png));
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_viewModel.InviteText is { } text)
        {
            await Clipboard.Default.SetTextAsync(text);
        }
    }
}

namespace Directo.App.Views;

/// <summary>Shown while local state is restored, or when it cannot be opened.</summary>
public sealed class StartupPage : ContentPage
{
    public StartupPage(string message)
    {
        Content = new VerticalStackLayout
        {
            Padding = 32,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { IsRunning = true },
                new Label { Text = message, HorizontalTextAlignment = TextAlignment.Center },
            },
        };
    }
}

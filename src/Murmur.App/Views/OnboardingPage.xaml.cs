using Murmur.Presentation.ViewModels;

namespace Murmur.App.Views;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.Completed += (_, _) =>
        {
            if (Window is { } window)
            {
                window.Page = services.GetRequiredService<AppShell>();
            }
        };
    }
}

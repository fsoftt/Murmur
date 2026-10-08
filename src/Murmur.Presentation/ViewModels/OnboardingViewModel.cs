using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;

namespace Murmur.Presentation.ViewModels;

/// <summary>First run: explains the three product rules before the user starts.</summary>
public sealed partial class OnboardingViewModel(MurmurClient client) : ViewModelBase
{
    public static IReadOnlyList<OnboardingRule> Rules { get; } =
    [
        new("1", "Los dos tenéis que estar online", "No hay buzón en ningún servidor: si la otra persona no está, tu mensaje espera en tu teléfono."),
        new("2", "Tus contactos ven tu IP", "Habláis directamente, sin intermediarios, así que cada uno ve la dirección del otro."),
        new("3", "No hay copia en la nube", "Si pierdes el teléfono, pierdes tu identidad, tus contactos y tu historial."),
    ];

    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    /// <summary>Raised once the identity is ready and the rules were acknowledged.</summary>
    public event EventHandler? Completed;

    [RelayCommand]
    private Task StartAsync() => RunAsync(async () =>
    {
        if (!string.IsNullOrWhiteSpace(ProfileName))
        {
            await client.SetProfileNameAsync(ProfileName);
        }

        await client.MarkOnboardedAsync();
        Completed?.Invoke(this, EventArgs.Empty);
    });
}

public sealed record OnboardingRule(string Number, string Title, string Text);

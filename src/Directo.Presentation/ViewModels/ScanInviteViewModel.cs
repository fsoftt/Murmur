using CommunityToolkit.Mvvm.ComponentModel;
using Directo.Client;
using Directo.Presentation.Services;

namespace Directo.Presentation.ViewModels;

/// <summary>Accepts an invite read from a QR code (or pasted, for accessibility).</summary>
public sealed partial class ScanInviteViewModel(DirectoClient client, INavigator navigator) : ViewModelBase
{
    private int _inProgress;

    [ObservableProperty]
    public partial string Status { get; set; } = "Apunta la cámara al código QR de la otra persona.";

    /// <summary>Called for every QR code detected; only the first one is processed.</summary>
    public async Task AcceptAsync(string inviteText)
    {
        if (Interlocked.Exchange(ref _inProgress, 1) == 1)
        {
            return;
        }

        try
        {
            Status = "Conectando con la otra persona…";
            await RunAsync(async () =>
            {
                var contact = await client.AcceptInviteAsync(inviteText);
                Status = $"Emparejado con {contact.DisplayName}.";
                await navigator.OpenPairedAsync(contact.Id);
            });
            if (ErrorMessage is not null)
            {
                Status = "Puedes volver a intentarlo.";
            }
        }
        finally
        {
            Interlocked.Exchange(ref _inProgress, 0);
        }
    }
}

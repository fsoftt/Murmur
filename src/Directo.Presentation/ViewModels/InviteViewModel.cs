using CommunityToolkit.Mvvm.ComponentModel;
using Directo.Client;
using Directo.Domain.Model;
using Directo.Presentation.Services;

namespace Directo.Presentation.ViewModels;

/// <summary>Shows this device's QR invite and reacts when someone pairs with it.</summary>
public sealed partial class InviteViewModel(DirectoClient client, IUiDispatcher dispatcher, INavigator navigator) : ViewModelBase, IDisposable
{
    private bool _subscribed;

    /// <summary>Text encoded in the QR code. Contains no secrets.</summary>
    [ObservableProperty]
    public partial string? InviteText { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? ExpiresAt { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    public Task CreateAsync() => RunAsync(async () =>
    {
        if (!_subscribed)
        {
            client.Pairing.ContactPaired += OnPaired;
            _subscribed = true;
        }

        var invite = await client.CreateInviteAsync();
        InviteText = invite.Text;
        ExpiresAt = invite.ExpiresAt;
        Status = "Pide a la otra persona que escanee este código. Ambos necesitáis conexión a Internet.";
    });

    public void Dispose()
    {
        if (_subscribed)
        {
            client.Pairing.ContactPaired -= OnPaired;
            _subscribed = false;
        }
    }

    private void OnPaired(object? sender, Contact contact) =>
        dispatcher.Post(async () =>
        {
            Status = $"Emparejado con {contact.DisplayName}. Comparad el código de seguridad para verificarlo.";
            InviteText = null;
            await navigator.OpenChatAsync(contact.Id);
        });
}

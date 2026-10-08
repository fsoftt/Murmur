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

    /// <summary>"mm:ss" until the invite expires.</summary>
    [ObservableProperty]
    public partial string Remaining { get; set; } = string.Empty;

    /// <summary>Fraction of the lifetime left, from 1 down to 0, for the countdown ring.</summary>
    [ObservableProperty]
    public partial double RemainingFraction { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsExpired { get; set; }

    private DateTimeOffset _createdAt;

    /// <summary>Called by the view every second.</summary>
    public void UpdateCountdown(DateTimeOffset now)
    {
        if (ExpiresAt is not { } expiresAt)
        {
            return;
        }

        var left = expiresAt - now;
        if (left <= TimeSpan.Zero)
        {
            left = TimeSpan.Zero;
            IsExpired = true;
            Status = "El código ha caducado. Genera uno nuevo.";
        }

        var total = expiresAt - _createdAt;
        Remaining = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        RemainingFraction = total > TimeSpan.Zero ? Math.Clamp(left / total, 0, 1) : 0;
    }

    public Task CreateAsync() => RunAsync(async () =>
    {
        if (!_subscribed)
        {
            client.Pairing.ContactPaired += OnPaired;
            _subscribed = true;
        }

        _createdAt = DateTimeOffset.UtcNow;
        var invite = await client.CreateInviteAsync();
        InviteText = invite.Text;
        ExpiresAt = invite.ExpiresAt;
        IsExpired = false;
        UpdateCountdown(_createdAt);
        Status = "Esperando a que lo escaneen…";
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
            await navigator.OpenPairedAsync(contact.Id);
        });
}

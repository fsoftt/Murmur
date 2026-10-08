using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Directo.Domain.Model;
using Directo.Presentation.Formatting;

namespace Directo.Presentation.ViewModels;

public sealed partial class MessageItemViewModel : ObservableObject
{
    public MessageItemViewModel(Message message)
    {
        Id = message.Id;
        Body = message.Body;
        IsOutgoing = message.Direction == MessageDirection.Outgoing;
        Time = (message.ReceivedAt ?? message.CreatedAt).ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        Status = message.Status;
        SortKey = (message.Lamport, message.CreatedAt, message.Id.Value);
    }

    /// <summary>Same total order as the database: Lamport time, then creation time, then id.</summary>
    public (long Lamport, DateTimeOffset CreatedAt, Guid Id) SortKey { get; }

    public MessageId Id { get; }

    public string Body { get; }

    public bool IsOutgoing { get; }

    public string Time { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIcon), nameof(StatusDescription), nameof(CanRetry))]
    public partial MessageStatus Status { get; set; }

    public string StatusIcon => IsOutgoing ? UserMessages.ForStatus(Status).Icon : string.Empty;

    public string StatusDescription => IsOutgoing ? UserMessages.ForStatus(Status).Description : string.Empty;

    public bool CanRetry => Status == MessageStatus.Failed;
}

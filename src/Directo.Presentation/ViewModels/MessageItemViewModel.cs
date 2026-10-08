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
        DisplayedAt = message.ReceivedAt ?? message.CreatedAt;
        Time = DisplayedAt.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        Status = message.Status;
        SortKey = (message.Lamport, message.CreatedAt, message.Id.Value);
    }

    /// <summary>Same total order as the database: Lamport time, then creation time, then id.</summary>
    public (long Lamport, DateTimeOffset CreatedAt, Guid Id) SortKey { get; }

    public MessageId Id { get; }

    public string Body { get; }

    public bool IsOutgoing { get; }

    public string Time { get; }

    public DateTimeOffset DisplayedAt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIcon), nameof(StatusDescription), nameof(CanRetry), nameof(IsPending), nameof(IsSent), nameof(IsDelivered), nameof(IsFailed))]
    public partial MessageStatus Status { get; set; }

    /// <summary>First bubble of a run of consecutive messages from the same side.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Corners), nameof(TopSpacing))]
    public partial bool IsFirstInGroup { get; set; } = true;

    /// <summary>Last bubble of a run; it carries the time and the delivery state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Corners), nameof(ShowMeta))]
    public partial bool IsLastInGroup { get; set; } = true;

    public bool IsPending => IsOutgoing && Status == MessageStatus.Pending;

    public bool IsSent => IsOutgoing && Status == MessageStatus.Sent;

    public bool IsDelivered => IsOutgoing && Status == MessageStatus.Delivered;

    public bool IsFailed => IsOutgoing && Status == MessageStatus.Failed;

    public bool ShowMeta => IsLastInGroup;

    public double TopSpacing => IsFirstInGroup ? 10 : 2;

    /// <summary>
    /// Bubble corners: the sender's side is tight at the bottom, and also at the top for every
    /// bubble after the first of a run, so a run reads as one block.
    /// </summary>
    public BubbleCorners Corners
    {
        get
        {
            const double Round = 20;
            const double Tight = 6;
            var top = IsFirstInGroup ? Round : Tight;
            return IsOutgoing
                ? new BubbleCorners(Round, top, Round, Tight)
                : new BubbleCorners(top, Round, Tight, Round);
        }
    }

    public string StatusIcon => IsOutgoing ? UserMessages.ForStatus(Status).Icon : string.Empty;

    public string StatusDescription => IsOutgoing ? UserMessages.ForStatus(Status).Description : string.Empty;

    public bool CanRetry => Status == MessageStatus.Failed;
}

/// <summary>Corner radii of a chat bubble, independent of the UI framework.</summary>
public readonly record struct BubbleCorners(double TopLeft, double TopRight, double BottomLeft, double BottomRight);

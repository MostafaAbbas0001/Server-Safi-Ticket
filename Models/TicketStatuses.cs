namespace Safi_Ticket.Models;

/// <summary>Status names as seeded in the Statuses table.</summary>
public static class TicketStatuses
{
    public const string Initiated = "Initiated";
    public const string InProgress = "In Progress";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    /// <summary>Closed and cancelled tickets can no longer be assigned or changed.</summary>
    public static bool IsFinished(string? statusName) => statusName is Closed or Cancelled;
}

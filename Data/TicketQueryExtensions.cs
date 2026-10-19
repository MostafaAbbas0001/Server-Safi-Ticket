using Safi_Ticket.Models;

namespace Safi_Ticket.Data;

/// <summary>Filters shared by the ticket list and the overview, so both count the same tickets.</summary>
public static class TicketQueryExtensions
{
    public static IQueryable<Ticket> NotDeleted(this IQueryable<Ticket> tickets) =>
        tickets.Where(ticket => !ticket.IsDeleted);

    public static IQueryable<Ticket> AssignedTo(this IQueryable<Ticket> tickets, int? userId) =>
        userId.HasValue ? tickets.Where(ticket => ticket.UserId == userId.Value) : tickets;

    /// <summary>Tickets created on or after <paramref name="startDate"/> and on or before <paramref name="endDate"/> (whole days, UTC).</summary>
    public static IQueryable<Ticket> CreatedBetween(
        this IQueryable<Ticket> tickets,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        var (start, exclusiveEnd) = ToUtcDayRange(startDate, endDate);

        if (start.HasValue)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAt >= start.Value);
        }

        if (exclusiveEnd.HasValue)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAt < exclusiveEnd.Value);
        }

        return tickets;
    }

    public static (DateTime? Start, DateTime? ExclusiveEnd) ToUtcDayRange(
        DateTime? startDate,
        DateTime? endDate
    ) =>
        (
            startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc)
                : null,
            endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date, DateTimeKind.Utc).AddDays(1)
                : null
        );
}

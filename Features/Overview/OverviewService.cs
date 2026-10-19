using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Features.Overview;

public sealed class OverviewService(ApplicationDbContext context)
{
    public async Task<TicketOverviewResponse> GetTicketOverviewAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? userId
    )
    {
        var tickets = context
            .Tickets.AsNoTracking()
            .NotDeleted()
            .AssignedTo(userId)
            .CreatedBetween(startDate, endDate);

        var statusCounts = await tickets
            .GroupBy(ticket => ticket.StatusId)
            .Select(group => new { StatusId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.StatusId, item => item.Count);

        var statuses = await context
            .Statuses.AsNoTracking()
            .OrderBy(status => status.Id)
            .Select(status => new { status.Id, status.Name })
            .ToListAsync();

        var dailyCounts = await tickets
            .GroupBy(ticket => ticket.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Date, item => item.Count);

        // Every day in the range gets a bar, including days with no tickets.
        var (start, exclusiveEnd) = TicketQueryExtensions.ToUtcDayRange(startDate, endDate);
        var firstDay =
            start ?? (dailyCounts.Count > 0 ? dailyCounts.Keys.Min() : DateTime.UtcNow.Date);
        var lastDay =
            exclusiveEnd?.AddDays(-1)
            ?? (dailyCounts.Count > 0 ? dailyCounts.Keys.Max() : firstDay);
        var daily = new List<DailyCountResponse>();

        for (var day = firstDay.Date; day <= lastDay.Date; day = day.AddDays(1))
        {
            daily.Add(
                new DailyCountResponse(
                    DateTime.SpecifyKind(day, DateTimeKind.Utc),
                    dailyCounts.GetValueOrDefault(day)
                )
            );
        }

        return new TicketOverviewResponse(
            start,
            exclusiveEnd?.AddDays(-1),
            statusCounts.Values.Sum(),
            statuses
                .Select(status => new StatusCountResponse(
                    status.Id,
                    status.Name,
                    statusCounts.GetValueOrDefault(status.Id)
                ))
                .ToList(),
            daily
        );
    }
}

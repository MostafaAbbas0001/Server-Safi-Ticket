using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Response;

namespace Safi_Ticket.Services
{
    public class OverviewService
    {
        private readonly ApplicationDbContext _context;

        public OverviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TicketOverviewResponse> GetTicketOverviewAsync(string? timeFrame, int? userId)
        {
            var normalizedTimeFrame = NormalizeTimeFrame(timeFrame);
            var startDate = GetStartDate(normalizedTimeFrame);
            var ticketsQuery = _context.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);

            if (userId.HasValue)
            {
                ticketsQuery = ticketsQuery.Where(ticket => ticket.UserId == userId.Value);
            }

            if (startDate.HasValue)
            {
                ticketsQuery = ticketsQuery.Where(ticket => ticket.CreatedAt >= startDate.Value);
            }

            var statusCounts = await ticketsQuery
                .GroupBy(ticket => ticket.StatusId)
                .Select(group => new { StatusId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.StatusId, item => item.Count);

            var statuses = await _context
                .Statuses.AsNoTracking()
                .OrderBy(status => status.Id)
                .Select(status => new TicketStatusOverviewResponse
                {
                    Id = status.Id,
                    Name = status.Name,
                    Count = statusCounts.GetValueOrDefault(status.Id),
                })
                .ToListAsync();

            return new TicketOverviewResponse
            {
                TimeFrame = normalizedTimeFrame,
                TotalCount = statusCounts.Values.Sum(),
                Statuses = statuses,
            };
        }

        private static string NormalizeTimeFrame(string? timeFrame)
        {
            return timeFrame?.Trim().ToLowerInvariant() switch
            {
                "today" => "today",
                "7d" => "7d",
                "30d" => "30d",
                _ => "all",
            };
        }

        private static DateTime? GetStartDate(string timeFrame)
        {
            var now = DateTime.UtcNow;

            return timeFrame switch
            {
                "today" => now.Date,
                "7d" => now.AddDays(-7),
                "30d" => now.AddDays(-30),
                _ => null,
            };
        }
    }
}

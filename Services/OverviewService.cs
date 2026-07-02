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

        public async Task<TicketOverviewResponse> GetTicketOverviewAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? userId
        )
        {
            var normalizedStartDate = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            var normalizedEndDate = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            var exclusiveEndDate = normalizedEndDate?.AddDays(1);
            var ticketsQuery = _context.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);

            if (userId.HasValue)
            {
                ticketsQuery = ticketsQuery.Where(ticket => ticket.UserId == userId.Value);
            }

            if (normalizedStartDate.HasValue)
            {
                ticketsQuery = ticketsQuery.Where(ticket => ticket.CreatedAt >= normalizedStartDate.Value);
            }

            if (exclusiveEndDate.HasValue)
            {
                ticketsQuery = ticketsQuery.Where(ticket => ticket.CreatedAt < exclusiveEndDate.Value);
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
                StartDate = normalizedStartDate,
                EndDate = normalizedEndDate,
                TotalCount = statusCounts.Values.Sum(),
                Statuses = statuses,
            };
        }
    }
}

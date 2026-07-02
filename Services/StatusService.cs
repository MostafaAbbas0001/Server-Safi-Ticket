using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Response;

namespace Safi_Ticket.Services
{
    public class StatusService
    {
        private readonly ApplicationDbContext _context;

        public StatusService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LookupItemResponse>> GetStatusesAsync()
        {
            return await _context
                .Statuses.AsNoTracking()
                .OrderBy(status => status.Id)
                .Select(status => new LookupItemResponse
                {
                    Id = status.Id,
                    Name = status.Name,
                })
                .ToListAsync();
        }
    }
}

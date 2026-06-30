using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StatusController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatuses()
        {
            var statuses = await _context
                .Statuses.OrderBy(status => status.Id)
                .Select(status => new { status.Id, status.Name })
                .ToListAsync();

            return Ok(statuses);
        }
    }
}

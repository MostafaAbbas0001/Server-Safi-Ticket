using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PriorityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PriorityController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPriorities()
        {
            var priorities = await _context.Priorities
                .OrderBy(priority => priority.Id)
                .Select(priority => new
                {
                    priority.Id,
                    Name = priority.Type
                })
                .ToListAsync();

            return Ok(priorities);
        }
    }
}

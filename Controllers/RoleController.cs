using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RoleController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .OrderBy(role => role.Id)
                .Select(role => new
                {
                    role.Id,
                    role.Name
                })
                .ToListAsync();

            return Ok(roles);
        }
    }
}

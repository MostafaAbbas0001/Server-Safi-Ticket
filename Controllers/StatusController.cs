using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Authorization;
using Safi_Ticket.Services;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowRoles("Admin", "Officer")]
    public class StatusController : ControllerBase
    {
        private readonly StatusService _statusService;

        public StatusController(StatusService statusService)
        {
            _statusService = statusService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatuses()
        {
            var statuses = await _statusService.GetStatusesAsync();

            return Ok(statuses);
        }
    }
}

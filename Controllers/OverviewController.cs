using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Authorization;
using Safi_Ticket.Services;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowRoles("Admin", "Officer")]
    public class OverviewController : ControllerBase
    {
        private readonly OverviewService _overviewService;

        public OverviewController(OverviewService overviewService)
        {
            _overviewService = overviewService;
        }

        [HttpGet("tickets")]
        public async Task<IActionResult> GetTicketOverview(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? userId
        )
        {
            var overview = await _overviewService.GetTicketOverviewAsync(startDate, endDate, userId);

            return Ok(overview);
        }
    }
}

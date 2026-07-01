using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Services;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OverviewController : ControllerBase
    {
        private readonly OverviewService _overviewService;

        public OverviewController(OverviewService overviewService)
        {
            _overviewService = overviewService;
        }

        [HttpGet("tickets")]
        public async Task<IActionResult> GetTicketOverview(
            [FromQuery] string? timeFrame,
            [FromQuery] int? userId
        )
        {
            var overview = await _overviewService.GetTicketOverviewAsync(timeFrame, userId);

            return Ok(overview);
        }
    }
}

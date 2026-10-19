using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Common.Auth;

namespace Safi_Ticket.Features.Overview;

[ApiController]
[Route("api/[controller]")]
[AllowRoles(Roles.Admin, Roles.Officer)]
public class OverviewController(OverviewService overviewService) : ControllerBase
{
    /// <summary>Status counts and tickets created per day. Officers only see their own tickets.</summary>
    [HttpGet("tickets")]
    public async Task<TicketOverviewResponse> GetTicketOverview(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? userId
    ) =>
        await overviewService.GetTicketOverviewAsync(
            startDate,
            endDate,
            User.IsAdmin() ? userId : User.GetUserId()
        );
}

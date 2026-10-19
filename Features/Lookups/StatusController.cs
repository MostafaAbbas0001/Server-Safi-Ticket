using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Common.Auth;

namespace Safi_Ticket.Features.Lookups;

[ApiController]
[Route("api/[controller]")]
[AllowRoles(Roles.Admin, Roles.Officer)]
public class StatusController(LookupService lookupService) : ControllerBase
{
    [HttpGet]
    public async Task<List<LookupItemResponse>> GetStatuses() =>
        await lookupService.GetStatusesAsync();
}

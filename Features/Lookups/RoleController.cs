using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Common.Auth;

namespace Safi_Ticket.Features.Lookups;

[ApiController]
[Route("api/[controller]")]
[AllowRoles(Roles.Admin)]
public class RoleController(LookupService lookupService) : ControllerBase
{
    [HttpGet]
    public async Task<List<LookupItemResponse>> GetRoles() => await lookupService.GetRolesAsync();
}

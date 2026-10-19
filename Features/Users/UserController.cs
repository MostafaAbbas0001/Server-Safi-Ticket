using Microsoft.AspNetCore.Mvc;
using Safi_Ticket.Common.Auth;

namespace Safi_Ticket.Features.Users;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowRoles(Roles.Admin)]
public class UserController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<List<UserListItemResponse>> GetUsers() => await userService.GetUsersAsync();

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        await userService.CreateUserAsync(request);
        return Created(string.Empty, "User created.");
    }
}

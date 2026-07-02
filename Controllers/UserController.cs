using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Authorization;
using Safi_Ticket.Data;
using Safi_Ticket.Models;
using Safi_Ticket.Services;

namespace Safi_Ticket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [AllowRoles("Admin")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserService _userService;

        public UserController(ApplicationDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .OrderBy(user => user.Id)
                .Select(user => new
                {
                    user.Id,
                    user.Name,
                    user.Email
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        [AllowRoles("Admin")]
        public async Task<ActionResult<string>> CreateUser(CreateUserRequest request)
        {
            try
            {
                var user = await _userService.CreateUserAsync(request);

                return Created(string.Empty, user);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }
    }
}

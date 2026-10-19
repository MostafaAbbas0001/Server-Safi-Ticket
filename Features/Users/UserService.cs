using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Common;
using Safi_Ticket.Data;
using Safi_Ticket.Models;

namespace Safi_Ticket.Features.Users;

public sealed class UserService(ApplicationDbContext context)
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public Task<List<UserListItemResponse>> GetUsersAsync() =>
        context
            .Users.AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserListItemResponse(user.Id, user.Name, user.Email))
            .ToListAsync();

    public async Task CreateUserAsync(CreateUserRequest request)
    {
        var email = request.Email.Trim();

        if (await context.Users.AnyAsync(user => user.Email.ToLower() == email.ToLower()))
        {
            throw AppException.BadRequest("Someone already has an account with this email.");
        }

        if (!await context.Roles.AnyAsync(role => role.Id == request.RoleId))
        {
            throw AppException.BadRequest("Choose a role for this staff member.");
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            RoleId = request.RoleId,
        };
        user.HashedPassword = _passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}

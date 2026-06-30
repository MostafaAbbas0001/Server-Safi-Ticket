using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;
using Safi_Ticket.Models;

namespace Safi_Ticket.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<string> CreateUserAsync(CreateUserRequest request)
        {
            var emailExists = await _context.Users.AnyAsync(user => user.Email == request.Email);

            if (emailExists)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }

            var roleExists = await _context.Roles.AnyAsync(role => role.Id == request.RoleId);

            if (!roleExists)
            {
                throw new InvalidOperationException("The selected role does not exist.");
            }

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                RoleId = request.RoleId,
            };

            user.HashedPassword = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return "User Created";
        }
    }
}

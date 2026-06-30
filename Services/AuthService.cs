using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Safi_Ticket.Data;
using Safi_Ticket.DTO.Request;
using Safi_Ticket.DTO.Response;
using Safi_Ticket.Models;

namespace Safi_Ticket.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailNotificationService _emailNotificationService;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            EmailNotificationService emailNotificationService
        )
        {
            _context = context;
            _configuration = configuration;
            _emailNotificationService = emailNotificationService;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<string> SignUpAsync(SignUpRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var emailAlreadyExists = await _context.Users.AnyAsync(u =>
                u.Email.Trim().ToLower() == email
            );

            if (emailAlreadyExists)
            {
                return "Email already exists.";
            }

            var user = new User
            {
                Name = request.Name,
                Email = email,
                PhoneNumber = request.PhoneNumber,

                // Assuming RoleId = 2 means normal user
                RoleId = 2,
            };

            user.HashedPassword = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return "User registered successfully.";
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var email = NormalizeEmail(request.Email);

            var user = await _context
                .Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email);

            if (user == null)
            {
                return null;
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.HashedPassword,
                request.Password
            );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return null;
            }

            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.Name,
            };
        }

        public async Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var email = NormalizeEmail(request.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("Email is required.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(existingUser =>
                existingUser.Email.Trim().ToLower() == email
            );

            if (user == null)
            {
                throw new InvalidOperationException("Invalid email.");
            }

            var resetCode = GenerateResetCode();
            var tokenHash = HashToken(resetCode);

            var existingTokens = await _context
                .PasswordResetTokens.Where(resetToken =>
                    resetToken.UserId == user.Id && resetToken.UsedAt == null
                )
                .ToListAsync();

            foreach (var existingToken in existingTokens)
            {
                existingToken.UsedAt = DateTime.UtcNow;
            }

            _context.PasswordResetTokens.Add(
                new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    CreatedAt = DateTime.UtcNow,
                }
            );

            await _context.SaveChangesAsync();

            await _emailNotificationService.SendPasswordResetCodeAsync(user, resetCode);

            return "Reset code sent. Please check your email.";
        }

        public async Task<string> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var token = request.Token.Trim();
            var newPassword = request.NewPassword.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Reset code is invalid.");
            }

            if (newPassword.Length < 6)
            {
                throw new InvalidOperationException("Password must be at least 6 characters.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(existingUser =>
                existingUser.Email.Trim().ToLower() == email
            );

            if (user == null)
            {
                throw new InvalidOperationException("Reset code is invalid.");
            }

            var tokenHash = HashToken(token);
            var resetToken = await _context.PasswordResetTokens.FirstOrDefaultAsync(existingToken =>
                existingToken.UserId == user.Id
                && existingToken.TokenHash == tokenHash
                && existingToken.UsedAt == null
            );

            if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Reset code is invalid or expired.");
            }

            user.HashedPassword = _passwordHasher.HashPassword(user, newPassword);
            resetToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return "Password has been reset successfully.";
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];
            var expireMinutes = Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.Name),
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateResetToken()
        {
            return Convert
                .ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string GenerateResetCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Safi_Ticket.Common;
using Safi_Ticket.Data;
using Safi_Ticket.Email;
using Safi_Ticket.Models;

namespace Safi_Ticket.Features.Auth;

public sealed class AuthService(
    ApplicationDbContext context,
    IConfiguration configuration,
    TicketNotifications notifications,
    ILogger<AuthService> logger
)
{
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(10);
    private readonly PasswordHasher<User> _passwordHasher = new();

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await context
            .Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email);

        if (
            user == null
            || _passwordHasher.VerifyHashedPassword(user, user.HashedPassword, request.Password)
                == PasswordVerificationResult.Failed
        )
        {
            throw AppException.Unauthorized("That email and password don't match an account.");
        }

        return new AuthResponse(GenerateJwtToken(user));
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw AppException.BadRequest("Enter the email address you sign in with.");
        }

        var user =
            await context.Users.FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email)
            ?? throw AppException.BadRequest("There's no account with that email address.");

        // Only the newest code works: earlier unused codes are retired.
        var openTokens = await context
            .PasswordResetTokens.Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();
        openTokens.ForEach(token => token.UsedAt = DateTime.UtcNow);

        var resetCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        context.PasswordResetTokens.Add(
            new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = HashToken(resetCode),
                ExpiresAt = DateTime.UtcNow.Add(ResetCodeLifetime),
                CreatedAt = DateTime.UtcNow,
            }
        );
        await context.SaveChangesAsync();

        try
        {
            await notifications.SendPasswordResetCodeAsync(user, resetCode);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Couldn't send a password reset email to user {UserId}.",
                user.Id
            );
            throw new AppException(
                StatusCodes.Status500InternalServerError,
                "The reset email couldn't be sent. Ask your administrator to check the email settings."
            );
        }

        return "We've emailed you a reset code.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var token = request.Token.Trim();
        var newPassword = request.NewPassword.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            throw AppException.BadRequest("That reset code isn't valid.");
        }

        if (newPassword.Length < 6)
        {
            throw AppException.BadRequest("Choose a password with at least 6 characters.");
        }

        var user =
            await context.Users.FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == email)
            ?? throw AppException.BadRequest("That reset code isn't valid.");

        var tokenHash = HashToken(token);
        var resetToken = await context.PasswordResetTokens.FirstOrDefaultAsync(t =>
            t.UserId == user.Id && t.TokenHash == tokenHash && t.UsedAt == null
        );

        if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            throw AppException.BadRequest(
                "That reset code is wrong or has expired. Request a new one."
            );
        }

        user.HashedPassword = _passwordHasher.HashPassword(user, newPassword);
        resetToken.UsedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return "Your password has been updated. Sign in with the new one.";
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims:
            [
                new Claim("nameid", user.Id.ToString()),
                new Claim("unique_name", user.Name),
                new Claim("email", user.Email),
                new Claim("role", user.Role.Name),
            ],
            expires: DateTime.UtcNow.AddMinutes(
                Convert.ToDouble(configuration["Jwt:ExpireMinutes"])
            ),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}

using Microsoft.AspNetCore.Mvc;

namespace Safi_Ticket.Features.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<AuthResponse> Login(LoginRequest request) =>
        await authService.LoginAsync(request);

    [HttpPost("forgot-password")]
    public async Task<string> ForgotPassword(ForgotPasswordRequest request) =>
        await authService.ForgotPasswordAsync(request);

    [HttpPost("reset-password")]
    public async Task<string> ResetPassword(ResetPasswordRequest request) =>
        await authService.ResetPasswordAsync(request);
}

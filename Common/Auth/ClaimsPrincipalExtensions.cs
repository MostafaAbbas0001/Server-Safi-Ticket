using System.Security.Claims;

namespace Safi_Ticket.Common.Auth;

/// <summary>The signed-in staff member performing an action, taken from their JWT.</summary>
public sealed record Actor(int UserId, bool IsAdmin);

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id, from the JWT "nameid" claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirst("nameid")?.Value, out var userId)
            ? userId
            : throw AppException.Unauthorized("Your session is missing a user id. Sign in again.");
    }

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);

    public static Actor ToActor(this ClaimsPrincipal principal) =>
        new(principal.GetUserId(), principal.IsAdmin());
}

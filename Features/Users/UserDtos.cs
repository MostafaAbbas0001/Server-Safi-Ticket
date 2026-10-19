using System.ComponentModel.DataAnnotations;

namespace Safi_Ticket.Features.Users;

public sealed record CreateUserRequest(
    [property: Required] string Name,
    [property: Required, EmailAddress] string Email,
    [property: Required] string PhoneNumber,
    [property: Required] string Password,
    [property: Required] int RoleId
);

public sealed record UserListItemResponse(int Id, string Name, string Email);

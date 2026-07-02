using Microsoft.AspNetCore.Authorization;

namespace Safi_Ticket.Authorization
{
    public sealed class AllowRolesAttribute : AuthorizeAttribute
    {
        public AllowRolesAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles);
        }
    }
}

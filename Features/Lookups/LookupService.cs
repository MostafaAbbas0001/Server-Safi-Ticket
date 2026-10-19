using Microsoft.EntityFrameworkCore;
using Safi_Ticket.Data;

namespace Safi_Ticket.Features.Lookups;

public sealed record LookupItemResponse(int Id, string Name);

/// <summary>The small id/name lists the app needs for pickers: statuses and roles.</summary>
public sealed class LookupService(ApplicationDbContext context)
{
    public Task<List<LookupItemResponse>> GetStatusesAsync() =>
        context
            .Statuses.AsNoTracking()
            .OrderBy(status => status.Id)
            .Select(status => new LookupItemResponse(status.Id, status.Name))
            .ToListAsync();

    public Task<List<LookupItemResponse>> GetRolesAsync() =>
        context
            .Roles.AsNoTracking()
            .OrderBy(role => role.Id)
            .Select(role => new LookupItemResponse(role.Id, role.Name))
            .ToListAsync();
}

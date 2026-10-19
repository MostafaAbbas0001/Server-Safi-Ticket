namespace Safi_Ticket.Features.Overview;

public sealed record TicketOverviewResponse(
    DateTime? StartDate,
    DateTime? EndDate,
    int TotalCount,
    List<StatusCountResponse> Statuses,
    List<DailyCountResponse> DailyTickets
);

public sealed record StatusCountResponse(int Id, string Name, int Count);

public sealed record DailyCountResponse(DateTime Date, int Count);

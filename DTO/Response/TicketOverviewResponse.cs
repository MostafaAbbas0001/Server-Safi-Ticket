namespace Safi_Ticket.DTO.Response
{
    public class TicketOverviewResponse
    {
        public string TimeFrame { get; set; } = "all";

        public int TotalCount { get; set; }

        public List<TicketStatusOverviewResponse> Statuses { get; set; } = new();
    }

    public class TicketStatusOverviewResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}

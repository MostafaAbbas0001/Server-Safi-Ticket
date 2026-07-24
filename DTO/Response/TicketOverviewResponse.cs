namespace Safi_Ticket.DTO.Response
{
    public class TicketOverviewResponse
    {
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int TotalCount { get; set; }

        public List<TicketStatusOverviewResponse> Statuses { get; set; } = new();

        public List<TicketDailyOverviewResponse> DailyTickets { get; set; } = new();
    }

    public class TicketStatusOverviewResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public class TicketDailyOverviewResponse
    {
        public DateTime Date { get; set; }

        public int Count { get; set; }
    }
}
